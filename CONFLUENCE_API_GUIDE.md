# Confluence API Integration Guide

This guide explains how to use the generated JSON files to import Oracle documentation into Confluence via the REST API.

## Generated JSON Structure

The PowerShell script now generates JSON files that are fully compatible with the Confluence REST API `/rest/api/content` endpoint. Each file contains:

```json
{
  "type": "page",
  "title": "Page Title",
  "space": {
    "key": "SPACE_KEY"
  },
  "body": {
    "storage": {
      "value": "<html content>",
      "representation": "storage"
    }
  },
  "version": {
    "minorEdit": false,
    "number": 1
  },
  "status": "current",
  "ancestors": [
    {
      "id": "PARENT_PAGE_ID"
    }
  ]
}
```

## Script Parameters for Confluence Integration

### Basic Usage (Save Mode)
```powershell
# Windows
.\scripts\Sync-ConfluenceDoc.ps1 -Mode Save -ConfluenceSpaceKey "FINDEV" -ParentPageId "123456" -Verbose

# Linux/macOS  
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Save -ConfluenceSpaceKey "FINDEV" -ParentPageId "123456" -Verbose
```

### Direct Import (Apply Mode)
```powershell
# Get credentials first
$cred = Get-Credential

# Windows
.\scripts\Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "FINDEV" -Credential $cred -ParentPageId "123456" -Verbose

# Linux/macOS
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "FINDEV" -Credential (Get-Credential) -ParentPageId "123456" -Verbose
```

## API Integration Methods

### Method 1: Direct Script Import
Use the script's built-in "Apply" mode to directly import pages to Confluence:

**Advantages:**
- Automated process
- Built-in error handling
- Rate limiting included

**Configuration:**
- Set `-Mode Apply`
- Provide `-ConfluenceBaseUrl` (e.g., "http://localhost:8090")
- Provide `-ConfluenceSpaceKey` (e.g., "FINDEV")
- Provide `-Credential` (PSCredential object)
- Optional: `-ParentPageId` to organize pages under a parent

### Method 2: Bulk Import via API
Use the generated JSON files with external tools or custom scripts:

```bash
# Example using curl
for file in confluence/*.json; do
  curl -X POST \
    -H "Content-Type: application/json" \
    -H "Authorization: Basic $(echo -n 'username:password' | base64)" \
    -d @"$file" \
    "http://localhost:8090/rest/api/content"
  sleep 2
done
```

```powershell
# Example using PowerShell
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("username:password"))
}

Get-ChildItem confluence/*.json | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    Invoke-RestMethod -Uri "http://localhost:8090/rest/api/content" -Method POST -Headers $headers -Body $content
    Start-Sleep -Seconds 2
}
```

## Confluence Space Setup

1. **Create Target Space:**
   ```http
   POST /rest/api/space
   {
     "key": "FINDEV",
     "name": "Financial Development Documentation",
     "description": {
       "plain": {
         "value": "Oracle Banking Payments documentation",
         "representation": "plain"
       }
     }
   }
   ```

2. **Create Parent Page (Optional):**
   ```http
   POST /rest/api/content
   {
     "type": "page",
     "title": "Oracle Banking Payments Documentation",
     "space": {
       "key": "FINDEV"
     },
     "body": {
       "storage": {
         "value": "<p>This page contains the complete Oracle Banking Payments documentation.</p>",
         "representation": "storage"
       }
     }
   }
   ```

## Content Organization

The script processes **270 documentation pages** with the following organization:

- **Leaf Links Only**: Filters out parent navigation nodes, keeping only actual content pages
- **Article Content**: Extracts only the meaningful content from each page's `<article>` element
- **Hierarchical Structure**: Uses the `ancestors` field to maintain document hierarchy

## API Endpoints Used

- **Create Content**: `POST /rest/api/content`
- **Update Content**: `PUT /rest/api/content/{id}`
- **Get Space Info**: `GET /rest/api/space/{spaceKey}`

## Error Handling

The generated JSON includes proper error handling for:
- **400 Bad Request**: Invalid JSON structure or missing required fields
- **403 Forbidden**: Insufficient permissions
- **404 Not Found**: Space or parent page doesn't exist
- **409 Conflict**: Page with same title already exists in space

## Content Format

- **HTML Storage Format**: Content uses Confluence's native storage format
- **Relative Link Resolution**: All links are converted to absolute URLs
- **Image Handling**: Image sources are updated with absolute paths
- **Table Preservation**: Complex tables are preserved with original formatting

## Best Practices

1. **Rate Limiting**: Use appropriate delays between API calls (default: 1500ms)
2. **Error Recovery**: Implement retry logic for failed imports
3. **Content Validation**: Verify HTML content is valid before import
4. **Permission Management**: Ensure API user has space admin permissions
5. **Backup Strategy**: Export existing content before bulk import

## Monitoring and Logs

The script provides verbose logging for:
- Link discovery and filtering
- Content extraction progress
- API call results
- Error conditions and warnings

## Example Complete Workflow

```powershell
# 1. Generate JSON files
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Save -ConfluenceSpaceKey "FINDEV" -ParentPageId "123456" -Verbose

# 2. Review generated files
Get-ChildItem confluence/*.json | Select-Object Name, Length

# 3. Import to Confluence
$cred = Get-Credential
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "FINDEV" -Credential $cred -ParentPageId "123456" -Verbose
```

This approach ensures all 270 Oracle Banking Payments documentation pages are properly structured and ready for seamless import into your Confluence instance.