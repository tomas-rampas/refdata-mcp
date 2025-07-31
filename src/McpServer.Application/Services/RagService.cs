using McpServer.Core.Entities;
using McpServer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Provides the main RAG (Retrieval-Augmented Generation) functionality for the banking reference data system.
/// Processes natural language queries by searching for relevant document chunks and generating contextual responses.
/// </summary>
public class RagService
{
    private readonly IVectorStore _vectorStore;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<RagService> _logger;
    private const int MaxRelevantChunks = 5;
    private const double MinimumSimilarityScore = 0.7;

    /// <summary>
    /// Initializes a new instance of the RagService with required dependencies.
    /// </summary>
    /// <param name="vectorStore">The vector store for semantic document search</param>
    /// <param name="llmClient">The LLM client for embeddings and response generation</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public RagService(
        IVectorStore vectorStore,
        ILlmClient llmClient,
        ILogger<RagService> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a user query through the RAG pipeline.
    /// Generates query embeddings, searches for relevant documents, builds context, and generates a response.
    /// </summary>
    /// <param name="query">The user's natural language query about banking reference data</param>
    /// <param name="userId">Optional user ID for tracking and personalization</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>A ChatRequest entity containing the query, response, and relevant document chunks</returns>
    public async Task<ChatRequest> ProcessQueryAsync(
        string query,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        _logger.LogInformation("Processing query: {Query}", query);

        try
        {
            // Step 1: Generate embedding for the query
            _logger.LogDebug("Generating embedding for query");
            var queryEmbedding = await _llmClient.GenerateEmbeddingAsync(query, cancellationToken);

            // Step 2: Search for similar chunks in vector store
            _logger.LogDebug("Searching for similar document chunks");
            var relevantChunks = await _vectorStore.SearchSimilarAsync(
                queryEmbedding,
                MaxRelevantChunks,
                (float)MinimumSimilarityScore,
                cancellationToken);

            var chunksList = relevantChunks.ToList();
            _logger.LogInformation("Found {ChunkCount} relevant chunks", chunksList.Count);

            // Step 3: Build context from relevant chunks
            var context = BuildContext(chunksList);

            // Step 4: Generate response using LLM with context
            _logger.LogDebug("Generating response with LLM");
            var response = await _llmClient.GenerateResponseAsync(
                query,
                context,
                cancellationToken);

            // Step 5: Create and return ChatRequest entity
            var chatRequest = new ChatRequest
            {
                Id = Guid.NewGuid().ToString(),
                Query = query,
                Response = response,
                RelevantChunks = chunksList,
                Timestamp = DateTime.UtcNow,
                UserId = userId ?? string.Empty
            };

            _logger.LogInformation("Successfully processed query with {ChunkCount} relevant chunks", chunksList.Count);
            return chatRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Builds a context string from relevant document chunks for the LLM to use in response generation.
    /// </summary>
    /// <param name="chunks">The relevant document chunks found through vector search</param>
    /// <returns>A formatted context string containing chunk content and metadata</returns>
    private string BuildContext(IList<DocumentChunk> chunks)
    {
        if (!chunks.Any())
            return "No relevant information found in the knowledge base.";

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Based on the following banking reference documents:");
        contextBuilder.AppendLine();

        foreach (var chunk in chunks)
        {
            contextBuilder.AppendLine($"--- Document: {chunk.Metadata?.GetValueOrDefault("Title", "Unknown")} ---");
            
            if (chunk.Metadata != null)
            {
                if (chunk.Metadata.TryGetValue("Department", out var dept))
                    contextBuilder.AppendLine($"Department: {dept}");
                
                if (chunk.Metadata.TryGetValue("DocumentType", out var docType))
                    contextBuilder.AppendLine($"Type: {docType}");
                
                if (chunk.Metadata.TryGetValue("EffectiveDate", out var effectiveDate))
                    contextBuilder.AppendLine($"Effective Date: {effectiveDate}");
            }
            
            contextBuilder.AppendLine();
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }
}