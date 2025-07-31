using McpServer.Core.Enums;
using System.Collections;

namespace McpServer.Core.Entities;

/// <summary>
/// Contains banking-specific metadata extracted from documents to enable categorization and filtering.
/// Stores department ownership, document classification, versioning, and compliance information.
/// </summary>
public class DocumentMetadata : Dictionary<string, object>
{
    public string Title 
    { 
        get => this.TryGetValue(nameof(Title), out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        set => this[nameof(Title)] = value;
    }
    
    public string Department 
    { 
        get => this.TryGetValue(nameof(Department), out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        set => this[nameof(Department)] = value;
    }
    
    public DocumentType DocumentType 
    { 
        get => this.TryGetValue(nameof(DocumentType), out var value) && value is DocumentType docType ? docType : DocumentType.ReferenceData;
        set => this[nameof(DocumentType)] = value;
    }
    
    public DateTime? EffectiveDate 
    { 
        get => this.TryGetValue(nameof(EffectiveDate), out var value) ? value as DateTime? : null;
        set 
        {
            if (value.HasValue)
                this[nameof(EffectiveDate)] = value.Value;
            else
                this.Remove(nameof(EffectiveDate));
        }
    }
    
    public string Version 
    { 
        get => this.TryGetValue(nameof(Version), out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        set => this[nameof(Version)] = value;
    }
}