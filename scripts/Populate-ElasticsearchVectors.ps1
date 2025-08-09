<#
.SYNOPSIS
    Populates Elasticsearch with vectorized content from Confluence pages for RAG operations.

.DESCRIPTION
    This script fetches all pages from a specified Confluence space via REST API, processes them using 
    semantic structure-aware chunking, generates embeddings using Ollama, and indexes them in Elasticsearch 
    for semantic search and RAG (Retrieval-Augmented Generation) capabilities.

.PARAMETER ConfluenceBaseUrl
    The base URL of your Confluence instance (e.g., "http://localhost:8090").

.PARAMETER SpaceKey
    The key of the Confluence space to process (e.g., "REF" or "ORACLE_DOCS").

.PARAMETER PersonalAccessToken
    A Personal Access Token for authenticating with the Confluence API. Required for API access.

.PARAMETER ElasticsearchUrl
    The URL of your Elasticsearch instance (e.g., "http://localhost:9200"). Default: "http://localhost:9200".

.PARAMETER IndexName
    The name of the Elasticsearch index to create/populate. Default: "confluence-vectors".

.PARAMETER OllamaUrl
    The URL of your Ollama instance for generating embeddings. Default: "http://localhost:11434".

.PARAMETER EmbeddingModel
    The Ollama model to use for generating embeddings. Default: "all-minilm".

.PARAMETER MaxChunkTokens
    Absolute maximum tokens per chunk (fallback only). Default: 2000. Semantic chunking prioritizes completeness.

.PARAMETER BatchSize
    Number of chunks to process in parallel for embedding generation. Default: 10.

.PARAMETER RequestDelay
    Delay in milliseconds between API requests to be respectful to services. Default: 1000.

.PARAMETER DryRun
    If specified, performs all processing but doesn't actually index to Elasticsearch. Useful for testing.

.PARAMETER Force
    If specified, recreates the Elasticsearch index even if it already exists, removing all existing data.

.PARAMETER Verbose
    Enables verbose logging for detailed progress tracking.

.EXAMPLE
    # Basic usage - process REF space and populate Elasticsearch
    .\Populate-ElasticsearchVectors.ps1 -SpaceKey "REF" -PersonalAccessToken "your_token_here" -Verbose

.EXAMPLE
    # Custom configuration with specific services
    .\Populate-ElasticsearchVectors.ps1 -ConfluenceBaseUrl "http://localhost:8090" -SpaceKey "ORACLE_DOCS" -PersonalAccessToken "your_token" -ElasticsearchUrl "http://localhost:9200" -IndexName "oracle-banking-docs" -Verbose

.EXAMPLE
    # Dry run to test without indexing
    .\Populate-ElasticsearchVectors.ps1 -SpaceKey "REF" -PersonalAccessToken "your_token" -DryRun -Verbose

.EXAMPLE
    # Force recreation of index
    .\Populate-ElasticsearchVectors.ps1 -SpaceKey "REF" -PersonalAccessToken "your_token" -Force -Verbose

.NOTES
    Requires PowerShell 7+ for cross-platform compatibility.
    
    Dependencies:
    - HtmlAgilityPack (automatically installed)
    - Active Confluence, Elasticsearch, and Ollama services
    
    Service Requirements:
    - Confluence: REST API access with read permissions
    - Elasticsearch: Index creation and document write permissions
    - Ollama: Embedding model (all-minilm) available and loaded
    
    Chunking Strategy:
    - Semantic chunking based on document structure (headers, sections, procedures)
    - Preserves complete procedures, tables, and code blocks
    - Maintains contextual integrity for technical documentation
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Base URL of your Confluence instance.")]
    [string]$ConfluenceBaseUrl = "http://localhost:8090",

    [Parameter(Mandatory = $true, HelpMessage = "Key of the Confluence space to process.")]
    [string]$SpaceKey,

    [Parameter(Mandatory = $true, HelpMessage = "Personal Access Token for Confluence API authentication.")]
    [string]$PersonalAccessToken,

    [Parameter(Mandatory = $false, HelpMessage = "URL of your Elasticsearch instance.")]
    [string]$ElasticsearchUrl = "http://localhost:9200",

    [Parameter(Mandatory = $false, HelpMessage = "Name of the Elasticsearch index to create/populate.")]
    [string]$IndexName = "confluence-vectors",

    [Parameter(Mandatory = $false, HelpMessage = "URL of your Ollama instance.")]
    [string]$OllamaUrl = "http://localhost:11434",

    [Parameter(Mandatory = $false, HelpMessage = "Ollama model for generating embeddings.")]
    [string]$EmbeddingModel = "all-minilm",

    [Parameter(Mandatory = $false, HelpMessage = "Absolute maximum tokens per chunk (fallback only).")]
    [int]$MaxChunkTokens = 2000,

    [Parameter(Mandatory = $false, HelpMessage = "Batch size for parallel embedding processing.")]
    [int]$BatchSize = 10,

    [Parameter(Mandatory = $false, HelpMessage = "Delay in milliseconds between API requests.")]
    [int]$RequestDelay = 1000,

    [Parameter(Mandatory = $false, HelpMessage = "Perform dry run without actually indexing to Elasticsearch.")]
    [switch]$DryRun,

    [Parameter(Mandatory = $false, HelpMessage = "Force recreation of Elasticsearch index.")]
    [switch]$Force
)

# --- SCRIPT SETUP AND DEPENDENCY HANDLING ---

# Function to ensure HtmlAgilityPack is installed and loaded - Cross-platform version
# (Reusing the same dependency management from Sync-ConfluenceDoc.ps1)
function Initialize-Dependency {
    Write-Verbose "Checking for HtmlAgilityPack dependency..."
    
    # Try to load HtmlAgilityPack if already available
    try {
        Add-Type -AssemblyName "HtmlAgilityPack" -ErrorAction SilentlyContinue
        Write-Verbose "HtmlAgilityPack loaded from GAC/system."
        return
    }
    catch {
        Write-Verbose "HtmlAgilityPack not available in system, trying package installation..."
    }

    # Cross-platform package installation  
    $platformWindows = if ($PSVersionTable.PSVersion.Major -ge 6) { $isWindows } else { $PSVersionTable.Platform -eq "Win32NT" -or $null -eq $PSVersionTable.Platform }
    $packageFound = $false
    
    if ($platformWindows) {
        # Windows: Use traditional PackageManagement
        if (-not (Get-Package -Name HtmlAgilityPack -ErrorAction SilentlyContinue)) {
            Write-Host "Installing HtmlAgilityPack via PackageManagement..." -ForegroundColor Yellow
            try {
                if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
                    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force
                }
                Install-Package -Name HtmlAgilityPack -Scope CurrentUser -Force -ErrorAction Stop
                $packageFound = $true
            }
            catch {
                Write-Warning "PackageManagement installation failed, trying dotnet approach..."
            }
        } else {
            $packageFound = $true
        }
        
        if ($packageFound) {
            try {
                $hapPackage = Get-Package -Name HtmlAgilityPack
                # Try different possible paths for cross-platform compatibility
                $possiblePaths = @(
                    "lib/netstandard2.0/HtmlAgilityPack.dll",
                    "lib\netstandard2.0\HtmlAgilityPack.dll",
                    "lib/net45/HtmlAgilityPack.dll",
                    "lib\net45\HtmlAgilityPack.dll"
                )
                
                $dllPath = $null
                foreach ($path in $possiblePaths) {
                    $testPath = Join-Path -Path $hapPackage.Source -ChildPath $path
                    if (Test-Path $testPath) {
                        $dllPath = $testPath
                        break
                    }
                }
                
                if ($dllPath) {
                    Add-Type -Path $dllPath
                    Write-Verbose "HtmlAgilityPack loaded successfully from '$dllPath'."
                    return
                }
            }
            catch {
                Write-Warning "Could not load from package location, trying dotnet approach..."
            }
        }
    }
    
    # Cross-platform: Use dotnet add package approach
    Write-Host "Installing HtmlAgilityPack via dotnet..." -ForegroundColor Yellow
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "PowerShellHtmlAgilityPack"
    $projectFile = Join-Path $tempDir "TempProject.csproj"
    
    try {
        # Create temporary directory and project
        if (-not (Test-Path $tempDir)) {
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        }
        
        # Create a minimal .csproj file
        $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="HtmlAgilityPack" Version="1.11.60" />
  </ItemGroup>
</Project>
"@
        $csprojContent | Out-File -FilePath $projectFile -Encoding utf8
        
        # Restore packages
        $restoreResult = & dotnet restore $projectFile 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed: $restoreResult"
        }
        
        # Find the HtmlAgilityPack.dll in the packages
        $packagesDir = Join-Path $tempDir "obj/project.assets.json"
        if (Test-Path $packagesDir) {
            # Try to find HtmlAgilityPack in common locations
            $nugetCache = if ($platformWindows) {
                Join-Path $env:USERPROFILE ".nuget/packages"
            } else {
                Join-Path $env:HOME ".nuget/packages"
            }
            
            $hapDir = Join-Path $nugetCache "htmlagilitypack"
            if (Test-Path $hapDir) {
                $versions = Get-ChildItem $hapDir | Sort-Object Name -Descending | Select-Object -First 1
                $dllPath = Join-Path $versions.FullName "lib/netstandard2.0/HtmlAgilityPack.dll"
                
                if (Test-Path $dllPath) {
                    Add-Type -Path $dllPath
                    Write-Verbose "HtmlAgilityPack loaded successfully from NuGet cache: '$dllPath'."
                    return
                }
            }
        }
        
        throw "Could not locate HtmlAgilityPack.dll after installation"
    }
    catch {
        Write-Error "Failed to install/load HtmlAgilityPack via dotnet. Error: $($_.Exception.Message)"
        Write-Host "Please install HtmlAgilityPack manually:" -ForegroundColor Red
        Write-Host "  On Windows: Install-Package HtmlAgilityPack -Scope CurrentUser" -ForegroundColor Yellow
        Write-Host "  On Linux/Mac: dotnet add package HtmlAgilityPack to any .NET project" -ForegroundColor Yellow
        throw
    }
    finally {
        # Cleanup
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# --- CROSS-PLATFORM SETUP ---

# Detect platform for cross-platform compatibility (use built-in variables in PS 6.0+)
if ($PSVersionTable.PSVersion.Major -ge 6) {
    # Use built-in automatic variables in PowerShell 6.0+
    $currentPlatformWindows = $isWindows
    $currentPlatformLinux = $isLinux
    $currentPlatformMacOS = $isMacOS
} else {
    # Fallback for older PowerShell versions
    $currentPlatformWindows = $PSVersionTable.Platform -eq "Win32NT" -or $null -eq $PSVersionTable.Platform
    $currentPlatformLinux = $PSVersionTable.Platform -eq "Unix" -and $PSVersionTable.OS -like "*Linux*"
    $currentPlatformMacOS = $PSVersionTable.Platform -eq "Unix" -and $PSVersionTable.OS -like "*Darwin*"
}

Write-Verbose "Platform detected - Windows: $currentPlatformWindows, Linux: $currentPlatformLinux, macOS: $currentPlatformMacOS"

# --- CONFLUENCE API FUNCTIONS ---

# Function to create authentication headers for Confluence API
function New-ConfluenceHeaders {
    param([string]$PersonalAccessToken)
    
    return @{
        "Authorization" = "Bearer $PersonalAccessToken"
        "Content-Type"  = "application/json"
        "Accept"        = "application/json"
    }
}

# Function to validate Confluence space access
function Test-ConfluenceSpace {
    param(
        [string]$BaseUrl,
        [string]$SpaceKey,
        [hashtable]$Headers
    )
    
    $spaceUrl = "$BaseUrl/rest/api/space/$SpaceKey"
    
    try {
        Write-Verbose "Validating access to Confluence space '$SpaceKey'..."
        $response = Invoke-RestMethod -Uri $spaceUrl -Method Get -Headers $Headers -ErrorAction Stop
        Write-Host "✓ Successfully connected to Confluence space: $($response.name)" -ForegroundColor Green
        return $response
    }
    catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "Unknown" }
        Write-Error "Failed to access Confluence space '$SpaceKey'. HTTP Status: $statusCode. Error: $($_.Exception.Message)"
        throw
    }
}

# --- SCRIPT INITIALIZATION ---

Write-Host "Initializing Elasticsearch Vector Population Script..." -ForegroundColor Cyan
Write-Host "Target Space: $SpaceKey" -ForegroundColor Cyan
Write-Host "Elasticsearch Index: $IndexName" -ForegroundColor Cyan
Write-Host "Platform: $($PSVersionTable.Platform) - OS: $($PSVersionTable.OS)" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE: No data will be indexed to Elasticsearch" -ForegroundColor Yellow
}

# Initialize dependencies
Write-Host "Initializing dependencies..." -ForegroundColor Cyan
Initialize-Dependency

# Validate required services accessibility
Write-Host "Validating service connectivity..." -ForegroundColor Cyan

# Test Confluence connectivity
$confluenceHeaders = New-ConfluenceHeaders -PersonalAccessToken $PersonalAccessToken
$spaceInfo = Test-ConfluenceSpace -BaseUrl $ConfluenceBaseUrl -SpaceKey $SpaceKey -Headers $confluenceHeaders

# Test Elasticsearch connectivity
try {
    Write-Verbose "Testing Elasticsearch connectivity..."
    $esResponse = Invoke-RestMethod -Uri "$ElasticsearchUrl/_cluster/health" -Method Get -ErrorAction Stop
    Write-Host "✓ Elasticsearch cluster status: $($esResponse.status)" -ForegroundColor Green
}
catch {
    Write-Error "Failed to connect to Elasticsearch at $ElasticsearchUrl. Error: $($_.Exception.Message)"
    throw
}

# Test Ollama connectivity and model availability
try {
    Write-Verbose "Testing Ollama connectivity and model availability..."
    $ollamaModels = Invoke-RestMethod -Uri "$OllamaUrl/api/tags" -Method Get -ErrorAction Stop
    $modelAvailable = $ollamaModels.models | Where-Object { $_.name -like "*$EmbeddingModel*" }
    
    if ($modelAvailable) {
        Write-Host "✓ Ollama embedding model '$EmbeddingModel' is available" -ForegroundColor Green
    } else {
        Write-Warning "Embedding model '$EmbeddingModel' not found in Ollama. Available models: $($ollamaModels.models.name -join ', ')"
        Write-Host "Attempting to pull model '$EmbeddingModel'..." -ForegroundColor Yellow
        
        # Try to pull the model
        $pullPayload = @{ model = $EmbeddingModel } | ConvertTo-Json
        $pullResponse = Invoke-RestMethod -Uri "$OllamaUrl/api/pull" -Method Post -Body $pullPayload -ContentType "application/json" -ErrorAction Stop
        Write-Host "✓ Model '$EmbeddingModel' pull initiated" -ForegroundColor Green
    }
}
catch {
    Write-Error "Failed to connect to Ollama at $OllamaUrl or validate model. Error: $($_.Exception.Message)"
    throw
}

Write-Host "✓ All service validations completed successfully!" -ForegroundColor Green
Write-Host ""

# Function to get all pages from a Confluence space with pagination support
function Get-ConfluencePages {
    param(
        [string]$BaseUrl,
        [string]$SpaceKey,
        [hashtable]$Headers
    )
    
    $allPages = @()
    $start = 0
    $limit = 50  # Confluence API default limit
    $hasMore = $true
    
    Write-Host "Fetching pages from Confluence space '$SpaceKey'..." -ForegroundColor Cyan
    
    while ($hasMore) {
        $pageUrl = "$BaseUrl/rest/api/content?spaceKey=$SpaceKey&start=$start&limit=$limit&expand=metadata,version"
        
        try {
            Write-Verbose "Fetching pages: start=$start, limit=$limit"
            $response = Invoke-RestMethod -Uri $pageUrl -Method Get -Headers $Headers -ErrorAction Stop
            
            $pages = $response.results | Where-Object { $_.type -eq "page" }
            $allPages += $pages
            
            # Check if there are more pages
            $hasMore = $response.size -eq $limit
            $start += $limit
            
            Write-Progress -Activity "Fetching Confluence Pages" -Status "Retrieved $($allPages.Count) pages so far..." -PercentComplete -1
            
            # Rate limiting
            if ($RequestDelay -gt 0) {
                Start-Sleep -Milliseconds $RequestDelay
            }
        }
        catch {
            Write-Error "Failed to fetch pages from Confluence. Error: $($_.Exception.Message)"
            throw
        }
    }
    
    Write-Progress -Activity "Fetching Confluence Pages" -Completed
    Write-Host "✓ Found $($allPages.Count) pages in space '$SpaceKey'" -ForegroundColor Green
    
    return $allPages
}

# Function to get detailed page content including HTML body
function Get-ConfluencePageContent {
    param(
        [string]$BaseUrl,
        [string]$PageId,
        [hashtable]$Headers
    )
    
    $contentUrl = "$BaseUrl/rest/api/content/$PageId?expand=body.storage,metadata,version,space"
    
    try {
        Write-Verbose "Fetching content for page ID: $PageId"
        $response = Invoke-RestMethod -Uri $contentUrl -Method Get -Headers $Headers -ErrorAction Stop
        return $response
    }
    catch {
        Write-Warning "Failed to fetch content for page ID $PageId. Error: $($_.Exception.Message)"
        return $null
    }
}

# --- SEMANTIC CONTENT PROCESSING FUNCTIONS ---

# Function to convert HTML content to clean text with structure preservation
function ConvertTo-CleanText {
    param([string]$HtmlContent)
    
    if ([string]::IsNullOrWhiteSpace($HtmlContent)) {
        return ""
    }
    
    try {
        $htmlDoc = New-Object HtmlAgilityPack.HtmlDocument
        $htmlDoc.LoadHtml($HtmlContent)
        
        # Remove script and style elements
        $scriptsAndStyles = $htmlDoc.DocumentNode.SelectNodes("//script | //style")
        if ($scriptsAndStyles) {
            foreach ($element in $scriptsAndStyles) {
                $element.Remove()
            }
        }
        
        # Extract meaningful text while preserving some structure
        $textContent = $htmlDoc.DocumentNode.InnerText
        
        # Clean up excessive whitespace while preserving paragraph breaks
        $textContent = $textContent -replace '\s+', ' '
        $textContent = $textContent -replace '\s*\n\s*', "`n"
        $textContent = $textContent.Trim()
        
        return $textContent
    }
    catch {
        Write-Warning "Failed to convert HTML to clean text: $($_.Exception.Message)"
        return $HtmlContent
    }
}

# Function to analyze HTML structure and identify logical sections
function Get-DocumentStructure {
    param([string]$HtmlContent)
    
    if ([string]::IsNullOrWhiteSpace($HtmlContent)) {
        return @()
    }
    
    try {
        $htmlDoc = New-Object HtmlAgilityPack.HtmlDocument
        $htmlDoc.LoadHtml($HtmlContent)
        
        $sections = @()
        
        # Find all heading elements (h1, h2, h3, h4, h5, h6)
        $headings = $htmlDoc.DocumentNode.SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6")
        
        if ($headings) {
            foreach ($heading in $headings) {
                $level = [int]$heading.Name.Substring(1)  # Extract number from h1, h2, etc.
                $title = $heading.InnerText.Trim()
                
                # Find content following this heading until next heading of same or higher level
                $content = Get-SectionContent -Heading $heading -Level $level
                
                if (-not [string]::IsNullOrWhiteSpace($content)) {
                    $sections += [PSCustomObject]@{
                        Title = $title
                        Level = $level
                        Content = $content.Trim()
                        Type = "Section"
                    }
                }
            }
        }
        
        # If no headings found, treat entire content as single section
        if ($sections.Count -eq 0) {
            $cleanContent = ConvertTo-CleanText -HtmlContent $HtmlContent
            if (-not [string]::IsNullOrWhiteSpace($cleanContent)) {
                $sections += [PSCustomObject]@{
                    Title = "Content"
                    Level = 1
                    Content = $cleanContent
                    Type = "FullPage"
                }
            }
        }
        
        return $sections
    }
    catch {
        Write-Warning "Failed to analyze document structure: $($_.Exception.Message)"
        return @()
    }
}

# Function to extract content for a specific section
function Get-SectionContent {
    param(
        [HtmlAgilityPack.HtmlNode]$Heading,
        [int]$Level
    )
    
    $content = @()
    $currentNode = $Heading.NextSibling
    
    while ($currentNode) {
        # Stop if we encounter another heading of same or higher level
        if ($currentNode.Name -match "^h[1-$Level]$") {
            break
        }
        
        # Collect content from this node
        if ($currentNode.NodeType -eq [HtmlAgilityPack.HtmlNodeType]::Element) {
            $nodeText = $currentNode.InnerText
            if (-not [string]::IsNullOrWhiteSpace($nodeText)) {
                $content += $nodeText.Trim()
            }
        }
        elseif ($currentNode.NodeType -eq [HtmlAgilityPack.HtmlNodeType]::Text) {
            $textContent = $currentNode.InnerText
            if (-not [string]::IsNullOrWhiteSpace($textContent)) {
                $content += $textContent.Trim()
            }
        }
        
        $currentNode = $currentNode.NextSibling
    }
    
    return ($content -join " ")
}

# Function to create semantic chunks from page content
function ConvertTo-SemanticChunks {
    param(
        [PSCustomObject]$Page,
        [string]$HtmlContent,
        [int]$MaxTokens = 2000
    )
    
    Write-Verbose "Processing page: $($Page.title)"
    
    # Analyze document structure
    $sections = Get-DocumentStructure -HtmlContent $HtmlContent
    
    $chunks = @()
    $chunkIndex = 0
    
    foreach ($section in $sections) {
        # Estimate token count (rough approximation: 4 characters per token)
        $estimatedTokens = [math]::Ceiling($section.Content.Length / 4)
        
        if ($estimatedTokens -le $MaxTokens) {
            # Section fits in one chunk
            $chunks += [PSCustomObject]@{
                PageId = $Page.id
                PageTitle = $Page.title
                SpaceKey = $Page.space.key
                SectionTitle = $section.Title
                SectionLevel = $section.Level
                Content = $section.Content
                ChunkIndex = $chunkIndex++
                ChunkType = $section.Type
                EstimatedTokens = $estimatedTokens
                CreatedDate = $Page.version.when
                ModifiedDate = $Page.version.when
            }
        }
        else {
            # Section is too large, split into paragraphs
            Write-Verbose "Section '$($section.Title)' is large ($estimatedTokens tokens), splitting into paragraphs"
            
            $paragraphs = $section.Content -split '\n\s*\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            $currentChunk = ""
            
            foreach ($paragraph in $paragraphs) {
                $paragraphTokens = [math]::Ceiling($paragraph.Length / 4)
                $currentChunkTokens = [math]::Ceiling($currentChunk.Length / 4)
                
                if ($currentChunkTokens + $paragraphTokens -le $MaxTokens -and -not [string]::IsNullOrWhiteSpace($currentChunk)) {
                    # Add paragraph to current chunk
                    $currentChunk += "`n`n$paragraph"
                }
                else {
                    # Start new chunk
                    if (-not [string]::IsNullOrWhiteSpace($currentChunk)) {
                        $chunks += [PSCustomObject]@{
                            PageId = $Page.id
                            PageTitle = $Page.title
                            SpaceKey = $Page.space.key
                            SectionTitle = $section.Title
                            SectionLevel = $section.Level
                            Content = $currentChunk.Trim()
                            ChunkIndex = $chunkIndex++
                            ChunkType = "Paragraph"
                            EstimatedTokens = [math]::Ceiling($currentChunk.Length / 4)
                            CreatedDate = $Page.version.when
                            ModifiedDate = $Page.version.when
                        }
                    }
                    $currentChunk = $paragraph
                }
            }
            
            # Add final chunk if it has content
            if (-not [string]::IsNullOrWhiteSpace($currentChunk)) {
                $chunks += [PSCustomObject]@{
                    PageId = $Page.id
                    PageTitle = $Page.title
                    SpaceKey = $Page.space.key
                    SectionTitle = $section.Title
                    SectionLevel = $section.Level
                    Content = $currentChunk.Trim()
                    ChunkIndex = $chunkIndex++
                    ChunkType = "Paragraph"
                    EstimatedTokens = [math]::Ceiling($currentChunk.Length / 4)
                    CreatedDate = $Page.version.when
                    ModifiedDate = $Page.version.when
                }
            }
        }
    }
    
    Write-Verbose "Created $($chunks.Count) semantic chunks for page '$($Page.title)'"
    return $chunks
}

# --- OLLAMA VECTORIZATION FUNCTIONS ---

# Function to generate embeddings for a single text input
function New-OllamaEmbedding {
    param(
        [string]$Text,
        [string]$OllamaUrl,
        [string]$Model
    )
    
    $payload = @{
        model = $Model
        input = $Text
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$OllamaUrl/api/embed" -Method Post -Body $payload -ContentType "application/json" -ErrorAction Stop
        return $response.embeddings[0]  # Return the first (and only) embedding
    }
    catch {
        Write-Warning "Failed to generate embedding for text (length: $($Text.Length)): $($_.Exception.Message)"
        return $null
    }
}

# Function to generate embeddings for multiple chunks with batch processing
function New-OllamaEmbeddings {
    param(
        [array]$Chunks,
        [string]$OllamaUrl,
        [string]$Model,
        [int]$BatchSize = 10,
        [int]$DelayMs = 1000
    )
    
    Write-Host "Generating embeddings for $($Chunks.Count) chunks using Ollama..." -ForegroundColor Cyan
    
    $processedChunks = @()
    $successCount = 0
    $failureCount = 0
    
    for ($i = 0; $i -lt $Chunks.Count; $i += $BatchSize) {
        $batchEnd = [math]::Min($i + $BatchSize - 1, $Chunks.Count - 1)
        $batch = $Chunks[$i..$batchEnd]
        
        Write-Progress -Activity "Generating Embeddings" -Status "Processing batch $([math]::Floor($i/$BatchSize) + 1) of $([math]::Ceiling($Chunks.Count/$BatchSize))" -PercentComplete (($i / $Chunks.Count) * 100)
        
        # Process batch in parallel
        $batchJobs = @()
        foreach ($chunk in $batch) {
            if (-not [string]::IsNullOrWhiteSpace($chunk.Content)) {
                $batchJobs += Start-Job -ScriptBlock {
                    param($chunkData, $url, $model)
                    
                    $payload = @{
                        model = $model
                        input = $chunkData.Content
                    } | ConvertTo-Json
                    
                    try {
                        $response = Invoke-RestMethod -Uri "$url/api/embed" -Method Post -Body $payload -ContentType "application/json" -ErrorAction Stop
                        return @{
                            Chunk = $chunkData
                            Embedding = $response.embeddings[0]
                            Success = $true
                        }
                    }
                    catch {
                        return @{
                            Chunk = $chunkData
                            Embedding = $null
                            Success = $false
                            Error = $_.Exception.Message
                        }
                    }
                } -ArgumentList $chunk, $OllamaUrl, $Model
            }
        }
        
        # Wait for all jobs to complete and collect results
        $batchResults = $batchJobs | Wait-Job | Receive-Job
        $batchJobs | Remove-Job
        
        # Process results
        foreach ($result in $batchResults) {
            if ($result.Success -and $result.Embedding) {
                $enrichedChunk = $result.Chunk.PSObject.Copy()
                $enrichedChunk | Add-Member -MemberType NoteProperty -Name "Embedding" -Value $result.Embedding
                $processedChunks += $enrichedChunk
                $successCount++
            }
            else {
                Write-Warning "Failed to generate embedding for chunk in page '$($result.Chunk.PageTitle)': $($result.Error)"
                $failureCount++
            }
        }
        
        # Rate limiting between batches
        if ($DelayMs -gt 0 -and $i + $BatchSize -lt $Chunks.Count) {
            Start-Sleep -Milliseconds $DelayMs
        }
    }
    
    Write-Progress -Activity "Generating Embeddings" -Completed
    Write-Host "✓ Generated embeddings: $successCount successful, $failureCount failed" -ForegroundColor Green
    
    return $processedChunks
}

# --- ELASTICSEARCH INTEGRATION FUNCTIONS ---

# Function to create or update the Elasticsearch index
function Initialize-ElasticsearchIndex {
    param(
        [string]$ElasticsearchUrl,
        [string]$IndexName,
        [bool]$ForceRecreate = $false
    )
    
    Write-Host "Initializing Elasticsearch index '$IndexName'..." -ForegroundColor Cyan
    
    # Check if index exists
    $indexExists = $false
    try {
        $response = Invoke-RestMethod -Uri "$ElasticsearchUrl/$IndexName" -Method Head -ErrorAction SilentlyContinue
        $indexExists = $true
        Write-Verbose "Index '$IndexName' already exists"
    }
    catch {
        Write-Verbose "Index '$IndexName' does not exist"
    }
    
    if ($indexExists -and $ForceRecreate) {
        Write-Host "Deleting existing index '$IndexName'..." -ForegroundColor Yellow
        try {
            Invoke-RestMethod -Uri "$ElasticsearchUrl/$IndexName" -Method Delete -ErrorAction Stop
            Write-Host "✓ Deleted existing index" -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to delete existing index: $($_.Exception.Message)"
            throw
        }
        $indexExists = $false
    }
    
    if (-not $indexExists) {
        Write-Host "Creating new index '$IndexName'..." -ForegroundColor Yellow
        
        $indexMapping = @{
            mappings = @{
                properties = @{
                    title = @{
                        type = "text"
                        analyzer = "standard"
                    }
                    content = @{
                        type = "text"
                        analyzer = "standard"
                    }
                    embedding = @{
                        type = "dense_vector"
                        dims = 384  # all-minilm embedding dimensions
                        index = $true
                        similarity = "cosine"
                    }
                    page_id = @{
                        type = "keyword"
                    }
                    page_title = @{
                        type = "text"
                        analyzer = "standard"
                    }
                    space_key = @{
                        type = "keyword"
                    }
                    section_title = @{
                        type = "text"
                        analyzer = "standard"
                    }
                    section_level = @{
                        type = "integer"
                    }
                    chunk_index = @{
                        type = "integer"
                    }
                    chunk_type = @{
                        type = "keyword"
                    }
                    estimated_tokens = @{
                        type = "integer"
                    }
                    created_date = @{
                        type = "date"
                        format = "date_time"
                    }
                    modified_date = @{
                        type = "date"
                        format = "date_time"
                    }
                    indexed_date = @{
                        type = "date"
                        format = "date_time"
                    }
                }
            }
            settings = @{
                number_of_shards = 1
                number_of_replicas = 0  # For local development
            }
        } | ConvertTo-Json -Depth 10
        
        try {
            $response = Invoke-RestMethod -Uri "$ElasticsearchUrl/$IndexName" -Method Put -Body $indexMapping -ContentType "application/json" -ErrorAction Stop
            Write-Host "✓ Created index '$IndexName' successfully" -ForegroundColor Green
            return $true
        }
        catch {
            Write-Error "Failed to create index '$IndexName': $($_.Exception.Message)"
            throw
        }
    }
    else {
        Write-Host "✓ Using existing index '$IndexName'" -ForegroundColor Green
        return $true
    }
}

# Function to add documents to Elasticsearch using bulk API
function Add-DocumentsToElasticsearch {
    param(
        [array]$Chunks,
        [string]$ElasticsearchUrl,
        [string]$IndexName,
        [int]$BatchSize = 100,
        [bool]$DryRun = $false
    )
    
    if ($DryRun) {
        Write-Host "🔍 DRY RUN: Would index $($Chunks.Count) chunks to Elasticsearch" -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "Indexing $($Chunks.Count) chunks to Elasticsearch..." -ForegroundColor Cyan
    
    $indexedCount = 0
    $errorCount = 0
    $currentTime = Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ"
    
    for ($i = 0; $i -lt $Chunks.Count; $i += $BatchSize) {
        $batchEnd = [math]::Min($i + $BatchSize - 1, $Chunks.Count - 1)
        $batch = $Chunks[$i..$batchEnd]
        
        Write-Progress -Activity "Indexing to Elasticsearch" -Status "Batch $([math]::Floor($i/$BatchSize) + 1) of $([math]::Ceiling($Chunks.Count/$BatchSize))" -PercentComplete (($i / $Chunks.Count) * 100)
        
        # Build bulk request body
        $bulkBody = @()
        foreach ($chunk in $batch) {
            # Generate unique document ID
            $docId = "$($chunk.PageId)-$($chunk.ChunkIndex)"
            
            # Index operation metadata
            $bulkBody += @{ index = @{ _index = $IndexName; _id = $docId } } | ConvertTo-Json -Compress
            
            # Document data
            $document = @{
                title = $chunk.PageTitle
                content = $chunk.Content
                embedding = $chunk.Embedding
                page_id = $chunk.PageId
                page_title = $chunk.PageTitle
                space_key = $chunk.SpaceKey
                section_title = $chunk.SectionTitle
                section_level = $chunk.SectionLevel
                chunk_index = $chunk.ChunkIndex
                chunk_type = $chunk.ChunkType
                estimated_tokens = $chunk.EstimatedTokens
                created_date = $chunk.CreatedDate
                modified_date = $chunk.ModifiedDate
                indexed_date = $currentTime
            }
            $bulkBody += $document | ConvertTo-Json -Compress
        }
        
        $bulkRequestBody = $bulkBody -join "`n" + "`n"
        
        try {
            $response = Invoke-RestMethod -Uri "$ElasticsearchUrl/_bulk" -Method Post -Body $bulkRequestBody -ContentType "application/x-ndjson" -ErrorAction Stop
            
            # Check for errors in the response
            if ($response.errors) {
                $errors = $response.items | Where-Object { $_.index.error }
                foreach ($error in $errors) {
                    Write-Warning "Indexing error for document $($error.index._id): $($error.index.error.reason)"
                    $errorCount++
                }
            }
            
            $successfulInBatch = $batch.Count - ($response.items | Where-Object { $_.index.error }).Count
            $indexedCount += $successfulInBatch
            
            Write-Verbose "Indexed batch: $successfulInBatch/$($batch.Count) successful"
        }
        catch {
            Write-Error "Failed to index batch starting at position ${i}: $($_.Exception.Message)"
            $errorCount += $batch.Count
        }
    }
    
    Write-Progress -Activity "Indexing to Elasticsearch" -Completed
    Write-Host "✓ Indexing completed: $indexedCount successful, $errorCount failed" -ForegroundColor Green
    
    return $indexedCount -gt 0
}

# --- MAIN ORCHESTRATION LOGIC ---

Write-Host "Starting main processing pipeline..." -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor White
$startTime = Get-Date

try {
    # Step 1: Initialize Elasticsearch index
    Write-Host ""
    Write-Host "STEP 1: Initialize Elasticsearch Index" -ForegroundColor Magenta
    Write-Host "=======================================" -ForegroundColor White
    $indexInitialized = Initialize-ElasticsearchIndex -ElasticsearchUrl $ElasticsearchUrl -IndexName $IndexName -ForceRecreate $Force
    
    if (-not $indexInitialized) {
        throw "Failed to initialize Elasticsearch index"
    }

    # Step 2: Fetch all pages from Confluence space
    Write-Host ""
    Write-Host "STEP 2: Fetch Pages from Confluence" -ForegroundColor Magenta
    Write-Host "====================================" -ForegroundColor White
    $pages = Get-ConfluencePages -BaseUrl $ConfluenceBaseUrl -SpaceKey $SpaceKey -Headers $confluenceHeaders
    
    if ($pages.Count -eq 0) {
        Write-Warning "No pages found in Confluence space '$SpaceKey'. Exiting."
        exit 0
    }

    # Step 3: Process pages and create semantic chunks
    Write-Host ""
    Write-Host "STEP 3: Process Pages and Create Semantic Chunks" -ForegroundColor Magenta
    Write-Host "=================================================" -ForegroundColor White
    
    $allChunks = @()
    $processedPages = 0
    $skippedPages = 0
    
    foreach ($page in $pages) {
        Write-Progress -Activity "Processing Pages" -Status "Processing page: $($page.title)" -PercentComplete (($processedPages / $pages.Count) * 100)
        
        try {
            # Get detailed page content
            $pageContent = Get-ConfluencePageContent -BaseUrl $ConfluenceBaseUrl -PageId $page.id -Headers $confluenceHeaders
            
            if ($pageContent -and $pageContent.body -and $pageContent.body.storage -and $pageContent.body.storage.value) {
                $htmlContent = $pageContent.body.storage.value
                
                # Create semantic chunks
                $pageChunks = ConvertTo-SemanticChunks -Page $pageContent -HtmlContent $htmlContent -MaxTokens $MaxChunkTokens
                
                if ($pageChunks.Count -gt 0) {
                    $allChunks += $pageChunks
                    Write-Verbose "Created $($pageChunks.Count) chunks from page '$($page.title)'"
                    $processedPages++
                }
                else {
                    Write-Warning "No chunks created for page '$($page.title)' - content may be empty"
                    $skippedPages++
                }
            }
            else {
                Write-Warning "No content found for page '$($page.title)' - skipping"
                $skippedPages++
            }
        }
        catch {
            Write-Warning "Failed to process page '$($page.title)': $($_.Exception.Message)"
            $skippedPages++
        }
        
        # Rate limiting between page requests
        if ($RequestDelay -gt 0) {
            Start-Sleep -Milliseconds $RequestDelay
        }
    }
    
    Write-Progress -Activity "Processing Pages" -Completed
    Write-Host "✓ Page processing completed: $processedPages processed, $skippedPages skipped" -ForegroundColor Green
    Write-Host "✓ Total chunks created: $($allChunks.Count)" -ForegroundColor Green
    
    if ($allChunks.Count -eq 0) {
        Write-Warning "No chunks were created. Exiting."
        exit 0
    }

    # Step 4: Generate embeddings using Ollama
    Write-Host ""
    Write-Host "STEP 4: Generate Embeddings with Ollama" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor White
    
    $chunksWithEmbeddings = New-OllamaEmbeddings -Chunks $allChunks -OllamaUrl $OllamaUrl -Model $EmbeddingModel -BatchSize $BatchSize -DelayMs $RequestDelay
    
    if ($chunksWithEmbeddings.Count -eq 0) {
        throw "No embeddings were generated successfully"
    }
    
    Write-Host "✓ Embedding generation completed: $($chunksWithEmbeddings.Count) chunks with embeddings" -ForegroundColor Green

    # Step 5: Index documents to Elasticsearch
    Write-Host ""
    Write-Host "STEP 5: Index Documents to Elasticsearch" -ForegroundColor Magenta
    Write-Host "=========================================" -ForegroundColor White
    
    $indexingSuccess = Add-DocumentsToElasticsearch -Chunks $chunksWithEmbeddings -ElasticsearchUrl $ElasticsearchUrl -IndexName $IndexName -BatchSize 100 -DryRun $DryRun
    
    if (-not $indexingSuccess -and -not $DryRun) {
        throw "Indexing to Elasticsearch failed"
    }

    # Step 6: Final summary and statistics
    Write-Host ""
    Write-Host "STEP 6: Process Summary" -ForegroundColor Magenta
    Write-Host "=======================" -ForegroundColor White
    
    $endTime = Get-Date
    $totalDuration = $endTime - $startTime
    
    # Calculate statistics
    $avgChunksPerPage = if ($processedPages -gt 0) { [math]::Round($allChunks.Count / $processedPages, 2) } else { 0 }
    $avgTokensPerChunk = if ($allChunks.Count -gt 0) { [math]::Round(($allChunks | Measure-Object -Property EstimatedTokens -Average).Average, 0) } else { 0 }
    $totalTokens = ($allChunks | Measure-Object -Property EstimatedTokens -Sum).Sum
    
    Write-Host ""
    Write-Host "📊 PROCESSING STATISTICS" -ForegroundColor Yellow
    Write-Host "========================" -ForegroundColor White
    Write-Host "Confluence Space: $SpaceKey" -ForegroundColor White
    Write-Host "Total Pages Found: $($pages.Count)" -ForegroundColor White
    Write-Host "Pages Processed: $processedPages" -ForegroundColor White
    Write-Host "Pages Skipped: $skippedPages" -ForegroundColor White
    Write-Host "Total Chunks Created: $($allChunks.Count)" -ForegroundColor White
    Write-Host "Chunks with Embeddings: $($chunksWithEmbeddings.Count)" -ForegroundColor White
    Write-Host "Average Chunks per Page: $avgChunksPerPage" -ForegroundColor White
    Write-Host "Average Tokens per Chunk: $avgTokensPerChunk" -ForegroundColor White
    Write-Host "Total Estimated Tokens: $totalTokens" -ForegroundColor White
    Write-Host "Elasticsearch Index: $IndexName" -ForegroundColor White
    Write-Host "Processing Time: $($totalDuration.Hours):$($totalDuration.Minutes.ToString('00')):$($totalDuration.Seconds.ToString('00'))" -ForegroundColor White
    Write-Host ""
    
    if ($DryRun) {
        Write-Host "🔍 DRY RUN COMPLETED SUCCESSFULLY" -ForegroundColor Yellow
        Write-Host "Run without -DryRun to actually index the data to Elasticsearch" -ForegroundColor Yellow
    }
    else {
        Write-Host "✅ PROCESSING COMPLETED SUCCESSFULLY" -ForegroundColor Green
        Write-Host "Vector database is ready for semantic search and RAG operations!" -ForegroundColor Green
        
        # Display some example queries
        Write-Host ""
        Write-Host "💡 Example Elasticsearch Queries:" -ForegroundColor Cyan
        Write-Host "Search by content: GET $ElasticsearchUrl/$IndexName/_search?q=content:\"network code\"" -ForegroundColor White
        Write-Host "Get all chunks from a page: GET $ElasticsearchUrl/$IndexName/_search?q=page_title:\"Purpose\"" -ForegroundColor White
        Write-Host "Vector similarity search: POST $ElasticsearchUrl/$IndexName/_search (with kNN query)" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor White
    
}
catch {
    Write-Host ""
    Write-Host "❌ PROCESSING FAILED" -ForegroundColor Red
    Write-Host "===================" -ForegroundColor White
    Write-Error "An error occurred during processing: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.Exception.StackTrace -ForegroundColor Red
    
    $endTime = Get-Date
    $totalDuration = $endTime - $startTime
    Write-Host ""
    Write-Host "Processing stopped after: $($totalDuration.Hours):$($totalDuration.Minutes.ToString('00')):$($totalDuration.Seconds.ToString('00'))" -ForegroundColor White
    
    exit 1
}

# End of script
Write-Host "Script execution completed." -ForegroundColor White