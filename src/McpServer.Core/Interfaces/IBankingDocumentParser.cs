using McpServer.Core.Entities;

namespace McpServer.Core.Interfaces;

/// <summary>
/// Defines the contract for parsing banking documents to extract structured content and metadata.
/// Specializes in identifying banking-specific information like departments, document types, and effective dates.
/// </summary>
public interface IBankingDocumentParser
{
    /// <summary>
    /// Asynchronously parses a document to extract its clean content and banking-specific metadata.
    /// Handles various document formats (PDF, Word, text) used in banking reference data.
    /// </summary>
    /// <param name="document">The document to parse containing raw content and source information.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A parsed document with clean content and extracted banking metadata.</returns>
    Task<ParsedDocument> ParseDocumentAsync(Document document, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Asynchronously parses document content to extract its clean content and banking-specific metadata.
    /// Handles various document formats (PDF, Word, text) used in banking reference data.
    /// </summary>
    /// <param name="content">The raw document content to parse.</param>
    /// <param name="sourcePath">The source path or identifier of the document.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A tuple containing the parsed content and extracted metadata.</returns>
    Task<(string parsedContent, DocumentMetadata metadata)> ParseDocumentAsync(string content, string sourcePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts banking-specific metadata from document content using pattern recognition.
    /// Identifies department names, document types (Policy/Procedure/ReferenceData), effective dates, and version information.
    /// </summary>
    /// <param name="content">The text content to analyze for metadata extraction.</param>
    /// <returns>Structured metadata containing banking-specific document attributes.</returns>
    DocumentMetadata ExtractMetadata(string content);
}

/// <summary>
/// Represents a document that has been parsed and enriched with banking-specific metadata.
/// Contains clean content ready for chunking and metadata for categorization and search.
/// </summary>
public class ParsedDocument
{
    public string Content { get; set; } = string.Empty;
    public DocumentMetadata Metadata { get; set; } = new();
}