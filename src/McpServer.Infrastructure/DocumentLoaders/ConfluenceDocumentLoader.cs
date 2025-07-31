using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.DocumentLoaders;

/// <summary>
/// Loads banking documentation pages from Confluence spaces using REST API.
/// Retrieves wikis, runbooks, and knowledge base articles for RAG system ingestion.
/// </summary>
public partial class ConfluenceDocumentLoader : IDocumentLoader
{
    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _apiToken;
    private readonly string _spaceKey;

    public ConfluenceDocumentLoader(HttpClient httpClient, string baseUrl, string username, string apiToken, string spaceKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _apiToken = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
        _spaceKey = spaceKey ?? throw new ArgumentNullException(nameof(spaceKey));

        // Configure authentication
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    /// <inheritdoc cref="IDocumentLoader.LoadDocumentsAsync"/>
    public async Task<IEnumerable<Document>> LoadDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();
        var start = 0;
        const int limit = 25;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/rest/api/content?spaceKey={_spaceKey}&type=page&expand=body.storage&start={start}&limit={limit}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var results = root.GetProperty("results").EnumerateArray();
            var hasMore = false;

            foreach (var page in results)
            {
                hasMore = true;
                var id = page.GetProperty("id").GetString() ?? "";
                var title = page.GetProperty("title").GetString() ?? "";
                var type = page.GetProperty("type").GetString() ?? "";
                
                var body = page.GetProperty("body")
                    .GetProperty("storage")
                    .GetProperty("value")
                    .GetString() ?? "";

                // Simple HTML to text conversion (in production, use proper HTML parser)
                var content = $"# {title}\n\n{StripHtml(body)}";
                documents.Add(new Document
                {
                    Id = $"confluence-{id}",
                    Content = content,
                    SourcePath = $"confluence://{_spaceKey}/{id}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Title"] = title,
                        ["Type"] = type,
                        ["SpaceKey"] = _spaceKey
                    }
                });
            }

            if (!hasMore || !root.TryGetProperty("_links", out var links) || 
                !links.TryGetProperty("next", out _))
                break;

            start += limit;
        }

        return documents;
    }

    private static string StripHtml(string html)
    {
        // Basic HTML stripping - in production, use HtmlAgilityPack or similar
        return HtmlTagRegex().Replace(html, " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Trim();
    }
}