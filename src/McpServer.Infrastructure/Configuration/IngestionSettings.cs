namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the document ingestion background service.
/// Controls ingestion frequency, text chunking parameters, and local document source paths.
/// </summary>
public class IngestionSettings
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public string LocalDocumentsPath { get; set; } = "/data/documents";
}