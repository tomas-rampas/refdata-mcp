namespace McpServer.Application.DTOs;

/// <summary>
/// Data Transfer Object for incoming chat requests from the client.
/// Contains the user's query for the banking reference data system.
/// </summary>
public class ChatRequestDto
{
    /// <summary>
    /// The user's natural language query about banking policies, procedures, or reference data.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Optional user identifier for tracking and personalization.
    /// </summary>
    public string? UserId { get; set; }
}