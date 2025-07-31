using McpServer.Application.DTOs;

namespace McpServer.Client.Services;

/// <summary>
/// Interface for API client operations
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Sends a chat query to the API
    /// </summary>
    /// <param name="request">The chat request</param>
    /// <returns>The chat response with answer and sources</returns>
    Task<ChatResponseDto> SendChatQueryAsync(ChatRequestDto request);

    /// <summary>
    /// Starts a new ingestion job
    /// </summary>
    /// <returns>The job ID of the started ingestion</returns>
    Task<string> StartIngestionAsync();

    /// <summary>
    /// Gets the current ingestion status
    /// </summary>
    /// <returns>The current ingestion status or null if no job is running</returns>
    Task<IngestionStatusDto?> GetIngestionStatusAsync();
}