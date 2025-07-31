using McpServer.Core.Enums;

namespace McpServer.Core.Entities;

/// <summary>
/// Tracks the status and progress of document ingestion operations from various sources.
/// Provides monitoring and error tracking for batch processing of banking reference documents.
/// </summary>
public class IngestionJob
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public IngestionStatus Status { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DocumentsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}