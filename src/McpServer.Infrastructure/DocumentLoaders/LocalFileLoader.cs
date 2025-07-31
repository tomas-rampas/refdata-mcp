using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.DocumentLoaders;

/// <summary>
/// Loads banking reference documents from the local file system for ingestion into the RAG system.
/// Scans directories recursively and supports common document formats used in banking operations.
/// </summary>
public class LocalFileLoader : IDocumentLoader
{
    private readonly string _basePath;
    private readonly HashSet<string> _supportedExtensions;

    public LocalFileLoader(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".pdf", ".doc", ".docx", ".md", ".json"
        };
    }

    /// <inheritdoc cref="IDocumentLoader.LoadDocumentsAsync"/>
    public async Task<IEnumerable<Document>> LoadDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();

        if (!Directory.Exists(_basePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {_basePath}");
        }

        var files = Directory.EnumerateFiles(_basePath, "*.*", SearchOption.AllDirectories)
            .Where(file => _supportedExtensions.Contains(Path.GetExtension(file)));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var relativePath = Path.GetRelativePath(_basePath, file);
                documents.Add(new Document
                {
                    Id = relativePath,
                    Content = content,
                    SourcePath = file,
                    Metadata = new Dictionary<string, object>
                    {
                        ["FileName"] = Path.GetFileName(file),
                        ["Extension"] = Path.GetExtension(file),
                        ["LastModified"] = File.GetLastWriteTimeUtc(file)
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                Console.WriteLine($"Error reading file {file}: {ex.Message}");
            }
        }

        return documents;
    }
}