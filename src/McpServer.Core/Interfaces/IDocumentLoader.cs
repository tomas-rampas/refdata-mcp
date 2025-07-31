namespace McpServer.Core.Interfaces;

/// <summary>
/// Defines the contract for loading documents from various external sources (file system, Jira, Confluence)
/// into the MCP-RAG Banking Reference Data System for processing and ingestion.
/// </summary>
public interface IDocumentLoader
{
    /// <summary>
    /// Asynchronously loads documents from the configured source and returns them in a standardized format.
    /// Each document includes content and metadata suitable for banking reference data processing.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A collection of documents with their content and source metadata.</returns>
    Task<IEnumerable<Document>> LoadDocumentsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document loaded from an external source, containing the raw content
/// and metadata needed for banking reference data processing and RAG operations.
/// </summary>
public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}