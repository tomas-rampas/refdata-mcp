# Web Page Document Loader Implementation

## Summary
I've successfully implemented a `WebPageDocumentLoader` class that can fetch and parse HTML content from web pages for RAG purposes.

## Features Implemented

### 1. WebPageDocumentLoader Class
Located at: `/src/McpServer.Infrastructure/DocumentLoaders/WebPageDocumentLoader.cs`

Key features:
- **HTTP Content Fetching**: Uses HttpClient with proper timeout and user agent configuration
- **HTML Parsing**: Leverages HtmlAgilityPack for robust HTML parsing
- **Content Cleaning**: Removes scripts, styles, comments, and navigation elements
- **Semantic Structure Preservation**: Maintains headers, paragraphs, lists, and tables
- **Metadata Extraction**: Captures title, description, keywords, author, and publish date

### 2. WebPageSettings Configuration
Located at: `/src/McpServer.Infrastructure/Configuration/WebPageSettings.cs`

Configuration options:
- `Urls`: List of web pages to load
- `TimeoutSeconds`: HTTP request timeout (default: 30)
- `UserAgent`: Custom user agent string

### 3. HTML to Text Conversion
The loader implements sophisticated HTML-to-text conversion:
- Preserves document structure with markdown-like formatting
- Handles headers (h1-h6) with proper formatting
- Maintains list structure (ul/ol) with bullet points
- Extracts table content in readable format
- Filters out navigation links and UI elements
- Decodes HTML entities properly

### 4. Error Handling
- Graceful handling of HTTP errors
- Timeout protection
- Continues processing other URLs if one fails
- Comprehensive logging for debugging

## Usage Example

```csharp
// In dependency injection setup:
services.AddHttpClient<WebPageDocumentLoader>();
services.AddSingleton<IDocumentLoader>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var logger = provider.GetRequiredService<ILogger<WebPageDocumentLoader>>();
    var urls = new[] 
    { 
        "https://example.com/banking-policies",
        "https://example.com/reference-data-guide"
    };
    return new WebPageDocumentLoader(httpClient, logger, urls);
});
```

## Additional Improvements
- Fixed the regex warning in ConfluenceDocumentLoader by using GeneratedRegexAttribute
- Added HtmlAgilityPack NuGet package (v1.11.57) for professional HTML parsing

## Benefits for RAG System
1. **Clean Text Extraction**: Removes all HTML noise while preserving semantic meaning
2. **Metadata Preservation**: Captures important page metadata for better context
3. **Banking Content Focus**: Filters out navigation and UI elements to focus on actual content
4. **Robust Error Handling**: Ensures the ingestion pipeline continues even if some pages fail
5. **Configurable**: Easy to add new URLs through configuration

The web loader is now ready to be integrated into the document ingestion pipeline alongside the existing file, Jira, and Confluence loaders.