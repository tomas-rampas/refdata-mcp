namespace McpServer.Core.Enums;

public enum IngestionStatus
{
    Pending,
    InProgress,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled
}