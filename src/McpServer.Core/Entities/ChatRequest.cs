namespace McpServer.Core.Entities;

/// <summary>
/// Represents a user's query to the RAG system and captures the complete interaction.
/// Stores the query, generated response, and relevant document chunks used for answer generation.
/// </summary>
public class ChatRequest
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<DocumentChunk> RelevantChunks { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
}