using McpServer.Core.Entities;
using McpServer.Core.Enums;
using McpServer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Orchestrates the document ingestion process for the banking reference data system.
/// Manages the pipeline of loading, parsing, chunking, embedding, and storing documents from various sources.
/// </summary>
public class IngestionService
{
    private readonly IEnumerable<IDocumentLoader> _documentLoaders;
    private readonly IBankingDocumentParser _documentParser;
    private readonly IChunkingService _chunkingService;
    private readonly ILlmClient _llmClient;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<IngestionService> _logger;

    /// <summary>
    /// Initializes a new instance of the IngestionService with all required dependencies.
    /// </summary>
    /// <param name="documentLoaders">Collection of document loaders for different sources (files, Jira, Confluence, web)</param>
    /// <param name="documentParser">Parser for extracting banking-specific metadata</param>
    /// <param name="chunkingService">Service for splitting documents into chunks</param>
    /// <param name="llmClient">LLM client for generating embeddings</param>
    /// <param name="vectorStore">Vector store for persisting embedded chunks</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public IngestionService(
        IEnumerable<IDocumentLoader> documentLoaders,
        IBankingDocumentParser documentParser,
        IChunkingService chunkingService,
        ILlmClient llmClient,
        IVectorStore vectorStore,
        ILogger<IngestionService> logger)
    {
        _documentLoaders = documentLoaders ?? throw new ArgumentNullException(nameof(documentLoaders));
        _documentParser = documentParser ?? throw new ArgumentNullException(nameof(documentParser));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a new document ingestion job across all configured document sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The completed IngestionJob entity with statistics and status</returns>
    public async Task<IngestionJob> StartIngestionAsync(CancellationToken cancellationToken = default)
    {
        var job = new IngestionJob
        {
            Id = Guid.NewGuid().ToString(),
            Source = "All Sources",
            Status = IngestionStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            DocumentsProcessed = 0
        };

        _logger.LogInformation("Starting ingestion job {JobId}", job.Id);

        try
        {
            var totalDocumentsProcessed = 0;
            var errors = new List<string>();

            // Process documents from each loader
            foreach (var loader in _documentLoaders)
            {
                var loaderName = loader.GetType().Name;
                _logger.LogInformation("Processing documents from {LoaderName}", loaderName);

                try
                {
                    var documents = await loader.LoadDocumentsAsync(cancellationToken);
                    var documentsList = documents.ToList();
                    
                    _logger.LogInformation("Loaded {DocumentCount} documents from {LoaderName}", 
                        documentsList.Count, loaderName);

                    foreach (var document in documentsList)
                    {
                        try
                        {
                            await ProcessDocumentAsync(document, cancellationToken);
                            totalDocumentsProcessed++;
                        }
                        catch (Exception ex)
                        {
                            var error = $"Error processing document {document.Id}: {ex.Message}";
                            _logger.LogError(ex, "Error processing document {DocumentId}", document.Id);
                            errors.Add(error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error loading documents from {loaderName}: {ex.Message}";
                    _logger.LogError(ex, "Error loading documents from {LoaderName}", loaderName);
                    errors.Add(error);
                }
            }

            // Update job status
            job.DocumentsProcessed = totalDocumentsProcessed;
            job.CompletedAt = DateTime.UtcNow;
            
            if (errors.Any())
            {
                job.Status = IngestionStatus.CompletedWithErrors;
                job.ErrorMessage = string.Join("; ", errors);
                _logger.LogWarning("Ingestion job {JobId} completed with {ErrorCount} errors", 
                    job.Id, errors.Count);
            }
            else
            {
                job.Status = IngestionStatus.Completed;
                _logger.LogInformation("Ingestion job {JobId} completed successfully", job.Id);
            }
        }
        catch (Exception ex)
        {
            job.Status = IngestionStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Ingestion job {JobId} failed", job.Id);
        }

        return job;
    }

    /// <summary>
    /// Processes a single document through the ingestion pipeline.
    /// </summary>
    /// <param name="document">The document to process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    private async Task ProcessDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing document {DocumentId}", document.Id);

        // Step 1: Parse document and extract metadata
        var (parsedContent, metadata) = await _documentParser.ParseDocumentAsync(
            document.Content, 
            document.SourcePath, 
            cancellationToken);

        // Merge extracted metadata with existing metadata
        var enrichedMetadata = new Dictionary<string, object>(document.Metadata ?? new Dictionary<string, object>());
        foreach (var (key, value) in metadata)
        {
            enrichedMetadata[key] = value;
        }

        // Step 2: Chunk the document
        var chunks = await _chunkingService.ChunkDocumentAsync(
            parsedContent, 
            cancellationToken: cancellationToken);

        var chunksList = chunks.ToList();
        _logger.LogDebug("Document {DocumentId} split into {ChunkCount} chunks", 
            document.Id, chunksList.Count);

        // Step 3: Process each chunk
        foreach (var textChunk in chunksList)
        {
            // Create DocumentChunk from TextChunk
            var documentMetadata = new DocumentMetadata();
            foreach (var kvp in enrichedMetadata)
            {
                documentMetadata[kvp.Key] = kvp.Value;
            }
            
            var documentChunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                SourceId = document.Id,
                Content = textChunk.Content,
                Metadata = documentMetadata,
                CreatedAt = DateTime.UtcNow
            };

            // Generate embedding for chunk
            _logger.LogDebug("Generating embedding for chunk {ChunkId}", documentChunk.Id);
            var embedding = await _llmClient.GenerateEmbeddingAsync(
                documentChunk.Content, 
                cancellationToken);
            
            documentChunk.Embedding = embedding;

            // Store chunk in vector store
            await _vectorStore.StoreEmbeddingAsync(documentChunk, cancellationToken);
        }

        _logger.LogInformation("Successfully processed document {DocumentId} into {ChunkCount} chunks", 
            document.Id, chunksList.Count);
    }
}