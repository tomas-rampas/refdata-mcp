namespace McpServer.Application.DTOs;

/// <summary>
/// Data Transfer Object for chat responses sent to the client.
/// Contains the generated answer and supporting information from the RAG system.
/// </summary>
public class ChatResponseDto
{
    /// <summary>
    /// Unique identifier for this chat interaction.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The original user query that was processed.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// The AI-generated response based on relevant banking reference documents.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// List of source documents that were used to generate the response.
    /// </summary>
    public List<SourceDocumentDto> Sources { get; set; } = new();

    /// <summary>
    /// Timestamp when the query was processed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional confidence score for the response (0-1).
    /// </summary>
    public double? ConfidenceScore { get; set; }
}

/// <summary>
/// Represents a source document that contributed to the chat response.
/// </summary>
public class SourceDocumentDto
{
    /// <summary>
    /// Title of the source document.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Department that owns this document.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Type of document (Policy, Procedure, ReferenceData).
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Path or identifier of the source document.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Relevant excerpt from the document.
    /// </summary>
    public string? Excerpt { get; set; }
}