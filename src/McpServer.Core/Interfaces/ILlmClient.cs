namespace McpServer.Core.Interfaces;

/// <summary>
/// Defines the contract for interacting with Large Language Models (LLMs) in the RAG system.
/// Handles both embedding generation for vector search and text generation for query responses.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Asynchronously generates a vector embedding for the given text using the configured LLM.
    /// The embedding captures semantic meaning for use in similarity search operations.
    /// </summary>
    /// <param name="text">The text to convert into a vector embedding.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A float array representing the text's vector embedding.</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    /// <summary>
    /// Asynchronously generates a natural language response based on a prompt and retrieved context.
    /// Combines the user's query with relevant banking reference data to produce accurate answers.
    /// </summary>
    /// <param name="prompt">The user's query or question to answer.</param>
    /// <param name="context">Relevant document chunks retrieved from the vector store.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A generated response incorporating the provided context.</returns>
    Task<string> GenerateResponseAsync(string prompt, string context, CancellationToken cancellationToken = default);
}