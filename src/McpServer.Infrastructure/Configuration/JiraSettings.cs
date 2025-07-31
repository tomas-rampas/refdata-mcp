namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Jira integration to retrieve banking reference data issues.
/// Defines authentication credentials and JQL queries for filtering relevant tickets.
/// </summary>
public class JiraSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string DefaultJql { get; set; } = "project = REF";
}