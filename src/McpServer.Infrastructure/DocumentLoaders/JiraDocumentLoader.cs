using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.DocumentLoaders;

/// <summary>
/// Loads banking reference data issues from Jira using REST API with JQL query support.
/// Retrieves policies, procedures, and reference data tickets for RAG system ingestion.
/// </summary>
public class JiraDocumentLoader : IDocumentLoader
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _apiToken;
    private readonly string _jqlQuery;

    public JiraDocumentLoader(HttpClient httpClient, string baseUrl, string username, string apiToken, string jqlQuery = "project = REF")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _apiToken = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
        _jqlQuery = jqlQuery;

        // Configure authentication
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    /// <inheritdoc cref="IDocumentLoader.LoadDocumentsAsync"/>
    public async Task<IEnumerable<Document>> LoadDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();
        var startAt = 0;
        const int maxResults = 50;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/rest/api/2/search?jql={Uri.EscapeDataString(_jqlQuery)}&startAt={startAt}&maxResults={maxResults}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var issues = root.GetProperty("issues").EnumerateArray();
            var hasMore = false;

            foreach (var issue in issues)
            {
                hasMore = true;
                var key = issue.GetProperty("key").GetString() ?? "";
                var fields = issue.GetProperty("fields");
                
                var summary = fields.GetProperty("summary").GetString() ?? "";
                var description = fields.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
                var project = fields.TryGetProperty("project", out var proj) ? proj.GetProperty("key").GetString() ?? "" : "";
                var issueType = fields.TryGetProperty("issuetype", out var type) ? type.GetProperty("name").GetString() ?? "" : "";
                
                var content = $"# {key}: {summary}\n\n{description}";
                documents.Add(new Document
                {
                    Id = key,
                    Content = content,
                    SourcePath = $"jira://{key}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Project"] = project,
                        ["IssueType"] = issueType,
                        ["Summary"] = summary
                    }
                });
            }

            if (!hasMore)
                break;

            startAt += maxResults;
        }

        return documents;
    }
}