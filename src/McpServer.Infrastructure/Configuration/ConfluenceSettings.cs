namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Confluence integration to retrieve banking documentation.
/// Defines authentication credentials and space filtering for wiki knowledge base access.
/// </summary>
public class ConfluenceSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8090";
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = "REF";
}