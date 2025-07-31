namespace McpServer.Application.DTOs;

/// <summary>
/// Data Transfer Object for ingestion job status information.
/// Provides details about the progress and results of document ingestion operations.
/// </summary>
public class IngestionStatusDto
{
    /// <summary>
    /// Unique identifier for the ingestion job.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// The source of documents being ingested (e.g., "All Sources", "Jira", "Confluence", "Local Files").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the ingestion job.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the ingestion job started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the ingestion job completed (null if still running).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Number of documents successfully processed.
    /// </summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>
    /// Error message if the job failed or had errors.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Estimated time remaining for the job (if still running).
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double? ProgressPercentage { get; set; }
}

/// <summary>
/// Data Transfer Object for starting a new ingestion job.
/// </summary>
public class StartIngestionDto
{
    /// <summary>
    /// Optional specific source to ingest from. If null, ingests from all sources.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Whether to force re-ingestion of already processed documents.
    /// </summary>
    public bool ForceReprocess { get; set; } = false;
}