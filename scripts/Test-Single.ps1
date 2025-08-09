<#
.SYNOPSIS
    Scrapes Oracle documentation and either saves it as Confluence JSON payloads or creates pages in a Confluence instance.

.DESCRIPTION
    This script is designed to be cross-platform (PowerShell 7+). It crawls the table of contents of a specified
    Oracle documentation site, extracts the main content from each page, and then, based on the selected mode,
    either saves a Confluence-ready JSON file or creates a page via the Confluence REST API.

.PARAMETER Mode
    Specifies the script's operation mode.
    - Save: Scrapes content and saves it as .json files in the directory specified by -OutputDirectory. (Default)
    - Apply: Scrapes content and creates pages directly in the target Confluence instance.

.PARAMETER SourceUrl
    The starting URL for the Oracle documentation's table of contents.

.PARAMETER OutputDirectory
    The local directory where JSON files will be saved when Mode is 'Save'.

.PARAMETER ConfluenceBaseUrl
    The base URL of your Confluence instance (e.g., "http://localhost:8090"). Required for 'Apply' mode.

.PARAMETER ConfluenceSpaceKey
    The key of the Confluence space where pages will be created (e.g., "FINDEV"). Required for 'Apply' mode.

.PARAMETER Credential
    A PSCredential object containing the username and password for the Confluence API. Required for 'Apply' mode.

.PARAMETER RequestDelay
    The delay in milliseconds between each web request to the Oracle server to be graceful.

.EXAMPLE
    # MODE 1: Scrape the docs and SAVE the JSON files locally (Windows)
    .\Sync-ConfluenceDoc.ps1 -Mode Save -Verbose

.EXAMPLE
    # MODE 1: Scrape the docs and SAVE the JSON files locally (Linux/macOS)
    pwsh ./Sync-ConfluenceDoc.ps1 -Mode Save -Verbose

.EXAMPLE
    # MODE 2: Scrape the docs and APPLY them directly to a local Confluence instance (Windows)
    $cred = Get-Credential
    .\Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "FINDEV" -Credential $cred -Verbose

.EXAMPLE
    # MODE 2: Scrape the docs and APPLY them directly to a local Confluence instance (Linux/macOS)
    $cred = Get-Credential
    pwsh ./Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "FINDEV" -Credential $cred -Verbose

.NOTES
    Requires PowerShell 7+ for cross-platform compatibility.
    
    Windows: Ensure your PowerShell execution policy allows running local scripts (e.g., Set-ExecutionPolicy -Scope Process RemoteSigned).
    Linux/macOS: Ensure you have .NET SDK installed for package management fallback.
    
    Dependencies:
    - HtmlAgilityPack (automatically installed)
    - .NET SDK (recommended for cross-platform package management)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Operation mode: Save or Apply.")]
    [ValidateSet('Save', 'Apply')]
    [string]$Mode = 'Save',

    [Parameter(Mandatory = $false, HelpMessage = "The starting URL for the Oracle documentation.")]
    [string]$SourceUrl = "https://docs.oracle.com/en/industries/financial-services/banking-payments/14.7.0.0.0/paycu/index.html",

    [Parameter(Mandatory = $false, HelpMessage = "The output directory for JSON files.")]
    [string]$OutputDirectory = "confluence",

    [Parameter(Mandatory = $false, HelpMessage = "Base URL of your Confluence instance.")]
    [string]$ConfluenceBaseUrl,

    [Parameter(Mandatory = $false, HelpMessage = "Key of the target Confluence space.")]
    [string]$ConfluenceSpaceKey,

    [Parameter(Mandatory = $false, HelpMessage = "Credentials for the Confluence API.")]
    [System.Management.Automation.PSCredential]$Credential,

    [Parameter(Mandatory = $false, HelpMessage = "Delay in milliseconds between requests.")]
    [int]$RequestDelay = 1500
)

# --- SCRIPT SETUP AND DEPENDENCY HANDLING ---

# Function to ensure HtmlAgilityPack is installed and loaded - Cross-platform version
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

# --- WEB SCRAPING FUNCTIONS ---

# Function to get all documentation page links from the main table of contents
function Get-DocumentationLinks {
    param([string]$BaseUrl)
    Write-Verbose "Fetching Table of Contents from $BaseUrl..."
    
    try {
        # Cross-platform web request with better error handling
        $webRequestParams = @{
            Uri = $BaseUrl
            UseBasicParsing = $true
            TimeoutSec = 30
            UserAgent = "Mozilla/5.0 (compatible; PowerShell Documentation Scraper)"
        }
        
        $response = Invoke-WebRequest @webRequestParams
        $htmlDoc = New-Object HtmlAgilityPack.HtmlDocument
        $htmlDoc.LoadHtml($response.Content)
    }
    catch {
        Write-Error "Failed to fetch content from $BaseUrl. Error: $($_.Exception.Message)"
        throw
    }
    
    $uniqueUrls = New-Object System.Collections.Generic.HashSet[string]
    $baseUri = New-Object System.Uri($BaseUrl)

    # First, check if there's a link to toc.htm (table of contents)
    $tocLinkNode = $htmlDoc.DocumentNode.SelectSingleNode("//a[@href='toc.htm']")
    if ($tocLinkNode) {
        Write-Verbose "Found table of contents link, fetching toc.htm..."
        $tocUrl = New-Object System.Uri($baseUri, "toc.htm")
        
        try {
            $tocResponse = Invoke-WebRequest -Uri $tocUrl.AbsoluteUri -UseBasicParsing -TimeoutSec 30
            $tocHtmlDoc = New-Object HtmlAgilityPack.HtmlDocument
            $tocHtmlDoc.LoadHtml($tocResponse.Content)
            
            # Get all links from the table of contents page - filter for leaf nodes only
            $tocLinkNodes = $tocHtmlDoc.DocumentNode.SelectNodes("//article//a[@href]")
            if ($tocLinkNodes) {
                Write-Verbose "Found $($tocLinkNodes.Count) total links in table of contents"
                
                # Filter to only leaf links (links that don't have child links)
                $leafLinks = @()
                foreach ($node in $tocLinkNodes) {
                    $href = $node.GetAttributeValue('href', '')
                    if ($href -and $href -ne '#' -and -not $href.StartsWith('javascript:')) {
                        # Check if this link has child links in the TOC structure
                        $parentLi = $node.ParentNode
                        while ($parentLi -and $parentLi.Name -ne 'li') {
                            $parentLi = $parentLi.ParentNode
                        }
                        
                        $hasChildLinks = $false
                        if ($parentLi) {
                            # Check if this li has nested ul/ol with more links
                            $childLists = $parentLi.SelectNodes(".//ul//a[@href] | .//ol//a[@href]")
                            if ($childLists -and $childLists.Count -gt 0) {
                                # Check if any child link is different from current link
                                foreach ($childLink in $childLists) {
                                    $childHref = $childLink.GetAttributeValue('href', '')
                                    if ($childHref -and $childHref -ne $href) {
                                        $hasChildLinks = $true
                                        break
                                    }
                                }
                            }
                        }
                        
                        # Only add if it's a leaf node (no child links)
                        if (-not $hasChildLinks) {
                            # Filter out non-HTML files and resolve relative URLs
                            if ($href.EndsWith('.html') -or $href.EndsWith('.htm') -or -not $href.Contains('.')) {
                                $absoluteUri = New-Object System.Uri($baseUri, $href)
                                $leafLinks += $absoluteUri.AbsoluteUri
                            }
                        }
                    }
                }
                
                # Add unique leaf links
                foreach ($leafLink in $leafLinks) {
                    $uniqueUrls.Add($leafLink) | Out-Null
                }
                Write-Verbose "Filtered to $($leafLinks.Count) leaf links"
            }
        }
        catch {
            Write-Warning "Failed to fetch table of contents from toc.htm: $($_.Exception.Message)"
        }
    }
    
    # Also check for any direct navigation links on the main page
    $xpathPrimaryNav = "//nav[@aria-label='Primary navigation']//a[@href]"
    $xpathTocContainer = "//div[contains(@class,'toc-container')]//a[@href]"
    $xpathGenericLinks = "//article//a[@href]"
    
    # Try multiple XPath patterns to find navigation links
    $xpathPatterns = @($xpathPrimaryNav, $xpathTocContainer, $xpathGenericLinks)
    
    foreach ($xpath in $xpathPatterns) {
        $linkNodes = $htmlDoc.DocumentNode.SelectNodes($xpath)
        if ($linkNodes) {
            Write-Verbose "Found $($linkNodes.Count) links using XPath: $xpath"
            foreach ($node in $linkNodes) {
                $href = $node.GetAttributeValue('href', '')
                if ($href -and $href -ne '#' -and -not $href.StartsWith('javascript:')) {
                    # Filter for content files and resolve relative URLs
                    if ($href.EndsWith('.html') -or $href.EndsWith('.htm') -or -not $href.Contains('.')) {
                        $absoluteUri = New-Object System.Uri($baseUri, $href)
                        $uniqueUrls.Add($absoluteUri.AbsoluteUri) | Out-Null
                    }
                }
            }
        }
    }
    
    Write-Verbose "Found $($uniqueUrls.Count) unique documentation pages."
    return $uniqueUrls
}

# Function to extract the main content and title from a single page
function Get-PageContent {
    param([string]$PageUrl)
    $baseUri = New-Object System.Uri($PageUrl)
    Write-Verbose "Scraping content from $PageUrl"
    
    try {
        # Cross-platform web request with retry logic
        $webRequestParams = @{
            Uri = $PageUrl
            UseBasicParsing = $true
            TimeoutSec = 30
            UserAgent = "Mozilla/5.0 (compatible; PowerShell Documentation Scraper)"
        }
        
        $response = Invoke-WebRequest @webRequestParams
        $htmlDoc = New-Object HtmlAgilityPack.HtmlDocument
        $htmlDoc.LoadHtml($response.Content)
    }
    catch {
        Write-Warning "Failed to fetch content from $PageUrl. Error: $($_.Exception.Message)"
        return $null
    }
    
    # Extract only the article element content - Oracle docs structure
    $articleNode = $htmlDoc.DocumentNode.SelectSingleNode("//article[@role='none']")
    
    if (-not $articleNode) {
        # Fallback to any article tag
        $articleNode = $htmlDoc.DocumentNode.SelectSingleNode("//article")
    }
    
    if (-not $articleNode) {
        Write-Warning "Could not find article element on page $PageUrl"
        return $null
    }

    # Fix relative links and image sources within the article
    $articleNode.SelectNodes(".//a[@href] | .//img[@src]") | ForEach-Object {
        if ($_.Name -eq 'a') {
            $attrName = 'href'
        } else {
            $attrName = 'src'
        }
        $relativeUrl = $_.GetAttributeValue($attrName, '')
        if ($relativeUrl -and -not $relativeUrl.StartsWith('http')) {
            $absoluteUrl = New-Object System.Uri($baseUri, $relativeUrl)
            $_.SetAttributeValue($attrName, $absoluteUrl.AbsoluteUri)
        }
    }

    # Extract title from article h1 or page title
    $titleNode = $articleNode.SelectSingleNode(".//h1")
    if (-not $titleNode) {
        $titleNode = $htmlDoc.DocumentNode.SelectSingleNode("//title")
    }
    
    $title = if ($titleNode) { 
        $titleNode.InnerText.Trim() 
    } else { 
        # Use URL as fallback title
        $uri = New-Object System.Uri($PageUrl)
        $pathWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($uri.LocalPath)
        if ($pathWithoutExtension) { 
            $pathWithoutExtension.Replace('-', ' ').Replace('_', ' ')
        } else { 
            "Untitled Document" 
        }
    }
    
    # Store only the HTML content from the div inside the article tag
    $contentDiv = $articleNode.SelectSingleNode(".//div")
    $htmlContent = if ($contentDiv) { 
        $contentDiv.InnerHtml 
    } else { 
        $articleNode.InnerHtml 
    }

    return [PSCustomObject]@{
        Title = $title
        HtmlContent = $htmlContent
    }
}

# --- CONFLUENCE AND FILE FUNCTIONS ---

# Function to create the JSON content (simplified - just store HTML and title)
function New-ContentPayload {
    param([string]$Title, [string]$HtmlContent, [string]$SpaceKey)
    $payload = [PSCustomObject]@{
        title = $Title
        content = $HtmlContent
    }
    return ConvertTo-Json -InputObject $payload -Depth 5
}

# Function to create the JSON payload for the Confluence API (kept for backward compatibility)
function New-ConfluencePayload {
    param([string]$Title, [string]$HtmlContent, [string]$SpaceKey)
    $payload = [PSCustomObject]@{
        type  = 'page'
        title = $Title
        space = @{
            key = $SpaceKey
        }
        body  = @{
            storage = @{
                value          = $HtmlContent
                representation = 'storage'
            }
        }
    }
    return ConvertTo-Json -InputObject $payload -Depth 5
}

# Function to save the payload to a local file - Cross-platform version
function Save-PayloadToFile {
    param([string]$Payload, [string]$Title, [string]$Directory)
    
    # Ensure directory exists (cross-platform)
    if (-not (Test-Path -Path $Directory)) {
        Write-Verbose "Creating output directory: $Directory"
        New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    }
    
    # Sanitize title to create a valid filename (cross-platform)
    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars() -join ''
    $regex = "[$([regex]::Escape($invalidChars))]"
    $sanitizedTitle = $Title -replace $regex, '_'
    
    # Remove any leading/trailing whitespace and limit length
    $sanitizedTitle = $sanitizedTitle.Trim()
    if ($sanitizedTitle.Length -gt 200) {
        $sanitizedTitle = $sanitizedTitle.Substring(0, 200)
    }
    
    $fileName = $sanitizedTitle + ".json"
    $filePath = Join-Path -Path $Directory -ChildPath $fileName

    Write-Verbose "Saving payload to $filePath"
    
    # Use cross-platform file writing with proper encoding
    try {
        $Payload | Out-File -FilePath $filePath -Encoding utf8 -NoNewline
        Write-Verbose "Successfully saved file: $filePath"
    }
    catch {
        Write-Error "Failed to save file '$filePath': $($_.Exception.Message)"
    }
}

# Function to post the payload to the Confluence API
function Invoke-ConfluenceApi {
    param([string]$Payload, [string]$BaseUrl, [System.Management.Automation.PSCredential]$Cred)
    $apiUrl = "$BaseUrl/rest/api/content"
    $username = $Cred.UserName
    $password = $Cred.GetNetworkCredential().Password
    $base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $username, $password)))
    
    $headers = @{
        "Authorization" = "Basic $base64AuthInfo"
        "Content-Type"  = "application/json"
    }

    try {
        Write-Verbose "Applying payload to Confluence API at $apiUrl"
        Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $Payload -ErrorAction Stop
        Write-Host "Successfully created page in Confluence." -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to create Confluence page. Response: $($_.Exception.Response.Content)"
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

# --- SCRIPT EXECUTION ---

Write-Host "Initializing dependencies..." -ForegroundColor Cyan
Initialize-Dependency

Write-Host "Starting Oracle Documentation Scraper..." -ForegroundColor Cyan
Write-Host "Mode: $Mode" -ForegroundColor Cyan
Write-Host "Platform: $($PSVersionTable.Platform) - OS: $($PSVersionTable.OS)" -ForegroundColor Cyan

# Validate required parameters for Apply mode
if ($Mode -eq 'Apply') {
    if (-not $ConfluenceBaseUrl) {
        Write-Error "ConfluenceBaseUrl is required when Mode is 'Apply'"
        exit 1
    }
    if (-not $ConfluenceSpaceKey) {
        Write-Error "ConfluenceSpaceKey is required when Mode is 'Apply'"
        exit 1
    }
    if (-not $Credential) {
        Write-Error "Credential is required when Mode is 'Apply'"
        exit 1
    }
    Write-Verbose "Apply mode parameters validated successfully"
}

# Validate prerequisites
if (-not $currentPlatformWindows) {
    # Check if dotnet is available on Linux/macOS
    $dotnetVersion = & dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Verbose "dotnet SDK version: $dotnetVersion"
    } else {
        Write-Warning "dotnet SDK not found. Some dependency installation methods may fail."
        Write-Host "Install .NET SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    }
}

$allLinks = Get-DocumentationLinks -BaseUrl $SourceUrl
$totalLinks = $allLinks.Count
$processedCount = 0

foreach ($link in $allLinks) {
    $processedCount++
    Write-Host "Processing page $processedCount of $totalLinks..." -ForegroundColor Yellow
    
    $pageData = Get-PageContent -PageUrl $link
    if ($pageData) {
        if ($Mode -eq 'Save') {
            $jsonPayload = New-ContentPayload -Title $pageData.Title -HtmlContent $pageData.HtmlContent -SpaceKey "DUMMY_KEY"
            Save-PayloadToFile -Payload $jsonPayload -Title $pageData.Title -Directory $OutputDirectory
        }
        elseif ($Mode -eq 'Apply') {
            $jsonPayload = New-ConfluencePayload -Title $pageData.Title -HtmlContent $pageData.HtmlContent -SpaceKey $ConfluenceSpaceKey
            Invoke-ConfluenceApi -Payload $jsonPayload -BaseUrl $ConfluenceBaseUrl -Cred $Credential
        }
    }

    Write-Verbose "Waiting for $RequestDelay ms before next request..."
    Start-Sleep -Milliseconds $RequestDelay
}

Write-Host "Scraping process complete!" -ForegroundColor Green
