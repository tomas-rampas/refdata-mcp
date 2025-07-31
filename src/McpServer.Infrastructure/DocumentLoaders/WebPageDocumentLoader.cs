using System.Text.RegularExpressions;
using HtmlAgilityPack;
using McpServer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.DocumentLoaders;

/// <summary>
/// Loads web page content from URLs and converts HTML to clean text suitable for RAG processing.
/// Extracts metadata, removes scripts/styles, and preserves semantic structure of banking documentation.
/// </summary>
public partial class WebPageDocumentLoader : IDocumentLoader
{
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();
    
    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultipleSpacesRegex();
    
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebPageDocumentLoader> _logger;
    private readonly List<string> _urls;

    public WebPageDocumentLoader(HttpClient httpClient, ILogger<WebPageDocumentLoader> logger, IEnumerable<string> urls)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _urls = urls?.ToList() ?? throw new ArgumentNullException(nameof(urls));

        // Configure HTTP client
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; McpRagBankingBot/1.0)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc cref="IDocumentLoader.LoadDocumentsAsync"/>
    public async Task<IEnumerable<Document>> LoadDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();

        foreach (var url in _urls)
        {
            try
            {
                _logger.LogInformation("Loading web page: {Url}", url);
                var document = await LoadWebPageAsync(url, cancellationToken);
                if (document != null)
                {
                    documents.Add(document);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web page {Url}", url);
            }
        }

        return documents;
    }

    private async Task<Document?> LoadWebPageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract metadata
            var metadata = ExtractMetadata(doc, url);
            
            // Convert to clean text
            var content = ConvertHtmlToText(doc);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("No content extracted from {Url}", url);
                return null;
            }

            return new Document
            {
                Id = url,
                Content = content,
                SourcePath = url,
                Metadata = metadata
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error loading {Url}", url);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout loading {Url}", url);
            return null;
        }
    }

    private Dictionary<string, object> ExtractMetadata(HtmlDocument doc, string url)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Url"] = url,
            ["LoadedAt"] = DateTime.UtcNow
        };

        // Extract title
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode != null)
        {
            metadata["Title"] = CleanText(titleNode.InnerText);
        }

        // Extract meta description
        var descriptionNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description']/@content");
        if (descriptionNode != null)
        {
            metadata["Description"] = CleanText(descriptionNode.GetAttributeValue("content", ""));
        }

        // Extract meta keywords
        var keywordsNode = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']/@content");
        if (keywordsNode != null)
        {
            metadata["Keywords"] = CleanText(keywordsNode.GetAttributeValue("content", ""));
        }

        // Extract author
        var authorNode = doc.DocumentNode.SelectSingleNode("//meta[@name='author']/@content");
        if (authorNode != null)
        {
            metadata["Author"] = CleanText(authorNode.GetAttributeValue("content", ""));
        }

        // Extract publish date
        var publishDateNode = doc.DocumentNode.SelectSingleNode("//meta[@property='article:published_time']/@content") 
                           ?? doc.DocumentNode.SelectSingleNode("//meta[@name='publish_date']/@content");
        if (publishDateNode != null)
        {
            var dateStr = publishDateNode.GetAttributeValue("content", "");
            if (DateTime.TryParse(dateStr, out var publishDate))
            {
                metadata["PublishDate"] = publishDate;
            }
        }

        return metadata;
    }

    private string ConvertHtmlToText(HtmlDocument doc)
    {
        // Remove script and style elements
        doc.DocumentNode.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style" || n.Name == "noscript")
            .ToList()
            .ForEach(n => n.Remove());

        // Remove comments
        doc.DocumentNode.Descendants()
            .Where(n => n.NodeType == HtmlNodeType.Comment)
            .ToList()
            .ForEach(n => n.Remove());

        var textNodes = new List<string>();
        ExtractTextFromNode(doc.DocumentNode, textNodes);

        // Join text with appropriate spacing
        var content = string.Join("\n", textNodes.Where(t => !string.IsNullOrWhiteSpace(t)));
        
        // Clean up excessive whitespace
        content = MultipleNewlinesRegex().Replace(content, "\n\n");
        content = MultipleSpacesRegex().Replace(content, " ");
        
        return content.Trim();
    }

    private void ExtractTextFromNode(HtmlNode node, List<string> textNodes)
    {
        // Handle text nodes
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = CleanText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                textNodes.Add(text);
            }
            return;
        }

        // Handle specific elements with semantic meaning
        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                var headerText = CleanText(node.InnerText);
                if (!string.IsNullOrWhiteSpace(headerText))
                {
                    textNodes.Add($"\n\n# {headerText}\n");
                }
                break;

            case "p":
            case "div":
            case "section":
            case "article":
                foreach (var child in node.ChildNodes)
                {
                    ExtractTextFromNode(child, textNodes);
                }
                textNodes.Add("\n");
                break;

            case "ul":
            case "ol":
                textNodes.Add("\n");
                foreach (var child in node.ChildNodes)
                {
                    if (string.Equals(child.Name, "li", StringComparison.OrdinalIgnoreCase))
                    {
                        var itemText = CleanText(child.InnerText);
                        if (!string.IsNullOrWhiteSpace(itemText))
                        {
                            textNodes.Add($"• {itemText}\n");
                        }
                    }
                }
                textNodes.Add("\n");
                break;

            case "table":
                ExtractTableText(node, textNodes);
                break;

            case "br":
                textNodes.Add("\n");
                break;

            case "a":
                // Include link text but skip navigation links
                var linkText = CleanText(node.InnerText);
                if (!string.IsNullOrWhiteSpace(linkText) && 
                    !IsNavigationLink(linkText))
                {
                    textNodes.Add(linkText);
                }
                break;

            default:
                // Process child nodes for other elements
                foreach (var child in node.ChildNodes)
                {
                    ExtractTextFromNode(child, textNodes);
                }
                break;
        }
    }

    private void ExtractTableText(HtmlNode tableNode, List<string> textNodes)
    {
        textNodes.Add("\n");
        
        var rows = tableNode.SelectNodes(".//tr");
        if (rows != null)
        {
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td|.//th");
                if (cells != null)
                {
                    var rowText = string.Join(" | ", cells.Select(c => CleanText(c.InnerText)));
                    if (!string.IsNullOrWhiteSpace(rowText))
                    {
                        textNodes.Add(rowText + "\n");
                    }
                }
            }
        }
        
        textNodes.Add("\n");
    }

    private static bool IsNavigationLink(string linkText)
    {
        var navigationTerms = new[] { "menu", "nav", "home", "about", "contact", "login", "logout", "sign in", "sign out" };
        var lowerText = linkText.ToLowerInvariant();
        return navigationTerms.Any(lowerText.Contains) || linkText.Length < 3;
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Remove excessive whitespace
        text = WhitespaceRegex().Replace(text, " ");
        
        return text.Trim();
    }
}