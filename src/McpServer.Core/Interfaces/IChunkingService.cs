namespace McpServer.Core.Interfaces;

/// <summary>
/// Defines the contract for splitting large documents into smaller, overlapping chunks
/// suitable for vector embedding and retrieval in the RAG system.
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Asynchronously splits document content into smaller chunks using a sliding window approach.
    /// Maintains context between chunks through configurable overlap to preserve semantic meaning.
    /// </summary>
    /// <param name="content">The document content to be chunked.</param>
    /// <param name="chunkSize">Maximum size of each chunk in characters (default: 1000).</param>
    /// <param name="overlap">Number of overlapping characters between consecutive chunks (default: 200).</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A collection of text chunks with position information for reconstruction.</returns>
    Task<IEnumerable<TextChunk>> ChunkDocumentAsync(string content, int chunkSize = 1000, int overlap = 200, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a segment of text extracted from a larger document during the chunking process.
/// Includes position information to maintain document structure and enable chunk reconstruction.
/// </summary>
public class TextChunk
{
    public string Content { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
}