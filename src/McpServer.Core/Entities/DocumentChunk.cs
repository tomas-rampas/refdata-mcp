namespace McpServer.Core.Entities;

/// <summary>
/// Represents a processed segment of a banking document stored in the vector database.
/// Contains the text content, its vector embedding, and associated metadata for RAG operations.
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DocumentMetadata Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}