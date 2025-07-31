using McpServer.Core.Entities;

namespace McpServer.Core.Interfaces;

/// <summary>
/// Defines the contract for storing and retrieving document chunks with vector embeddings.
/// Enables semantic search capabilities for the RAG system using vector similarity.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Asynchronously stores a document chunk with its vector embedding in the vector database.
    /// Preserves metadata for filtering and maintains the embedding for similarity search.
    /// </summary>
    /// <param name="chunk">The document chunk containing content, embedding, and metadata to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>The unique identifier assigned to the stored chunk.</returns>
    Task<string> StoreEmbeddingAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    /// <summary>
    /// Asynchronously searches for document chunks most similar to the query embedding.
    /// Uses vector similarity (e.g., cosine similarity) to find semantically related content.
    /// </summary>
    /// <param name="queryEmbedding">The vector embedding of the search query.</param>
    /// <param name="topK">Maximum number of similar chunks to return (default: 5).</param>
    /// <param name="minimumSimilarityScore">Minimum similarity score threshold for results (default: 0.0).</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A collection of the most similar document chunks ordered by relevance.</returns>
    Task<IEnumerable<DocumentChunk>> SearchSimilarAsync(float[] queryEmbedding, int topK = 5, float minimumSimilarityScore = 0.0f, CancellationToken cancellationToken = default);
}