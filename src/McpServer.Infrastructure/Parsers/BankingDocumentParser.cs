using System.Text.RegularExpressions;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.Parsers;

/// <summary>
/// Parses banking documents to extract structured content and regulatory metadata.
/// Uses pattern recognition to identify departments, document types, versions, and compliance dates.
/// </summary>
public class BankingDocumentParser : IBankingDocumentParser
{
    private static readonly Regex DepartmentRegex = new(@"(?:Department|Dept)[:|\s]+([A-Za-z\s&]+)", RegexOptions.IgnoreCase);
    private static readonly Regex EffectiveDateRegex = new(@"(?:Effective Date|Effective)[:|\s]+(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})", RegexOptions.IgnoreCase);
    private static readonly Regex VersionRegex = new(@"(?:Version|Ver|V)[:|\s]+([\d.]+)", RegexOptions.IgnoreCase);
    
    private static readonly Dictionary<string, DocumentType> DocumentTypeKeywords = new()
    {
        { "policy", DocumentType.Policy },
        { "policies", DocumentType.Policy },
        { "procedure", DocumentType.Procedure },
        { "procedures", DocumentType.Procedure },
        { "reference", DocumentType.ReferenceData },
        { "lookup", DocumentType.ReferenceData },
        { "mapping", DocumentType.ReferenceData },
        { "codes", DocumentType.ReferenceData }
    };

    /// <inheritdoc cref="IBankingDocumentParser.ParseDocumentAsync"/>
    public Task<ParsedDocument> ParseDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        // For now, return content as-is. In production, implement proper parsing for PDF, Word, etc.
        var metadata = ExtractMetadata(document.Content);
        
        return Task.FromResult(new ParsedDocument
        {
            Content = document.Content,
            Metadata = metadata
        });
    }

    /// <inheritdoc cref="IBankingDocumentParser.ParseDocumentAsync"/>
    public Task<(string parsedContent, DocumentMetadata metadata)> ParseDocumentAsync(string content, string sourcePath, CancellationToken cancellationToken = default)
    {
        // For now, return content as-is. In production, implement proper parsing for PDF, Word, etc.
        var metadata = ExtractMetadata(content);
        
        return Task.FromResult((content, metadata));
    }

    /// <inheritdoc cref="IBankingDocumentParser.ExtractMetadata"/>
    public DocumentMetadata ExtractMetadata(string content)
    {
        var metadata = new DocumentMetadata
        {
            Title = ExtractTitle(content),
            Department = ExtractDepartment(content),
            DocumentType = ClassifyDocumentType(content),
            EffectiveDate = ExtractEffectiveDate(content),
            Version = ExtractVersion(content)
        };

        return metadata;
    }

    private string ExtractTitle(string content)
    {
        // Extract first non-empty line as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            // Remove common markdown headers
            firstLine = firstLine.TrimStart('#', ' ');
            return firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
        }
        return "Untitled Document";
    }

    private string ExtractDepartment(string content)
    {
        var match = DepartmentRegex.Match(content);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Fallback: check for common department names
        var departments = new[] { "Risk", "Compliance", "Operations", "Technology", "Finance", "Legal" };
        foreach (var dept in departments)
        {
            if (content.Contains(dept, StringComparison.OrdinalIgnoreCase))
            {
                return dept;
            }
        }

        return "General";
    }

    private DocumentType ClassifyDocumentType(string content)
    {
        var lowerContent = content.ToLower();
        
        foreach (var kvp in DocumentTypeKeywords)
        {
            if (lowerContent.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        // Default classification based on content patterns
        if (lowerContent.Contains("must") || lowerContent.Contains("shall") || lowerContent.Contains("required"))
        {
            return DocumentType.Policy;
        }
        
        if (lowerContent.Contains("step") || lowerContent.Contains("process") || lowerContent.Contains("how to"))
        {
            return DocumentType.Procedure;
        }

        return DocumentType.ReferenceData;
    }

    private DateTime? ExtractEffectiveDate(string content)
    {
        var match = EffectiveDateRegex.Match(content);
        if (match.Success)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }
        return null;
    }

    private string ExtractVersion(string content)
    {
        var match = VersionRegex.Match(content);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "1.0";
    }
}