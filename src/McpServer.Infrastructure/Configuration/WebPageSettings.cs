namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for web page document loading.
/// Defines URLs to crawl and HTTP client settings for retrieving banking documentation from web sources.
/// </summary>
public class WebPageSettings
{
    public List<string> Urls { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; McpRagBankingBot/1.0)";
}