using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.BackgroundServices;

/// <summary>
/// Runs periodic document ingestion from all configured sources (file system, Jira, Confluence).
/// Orchestrates the full pipeline: load, parse, chunk, embed, and store documents for RAG retrieval.
/// </summary>
public class IngestionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public IngestionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<IngestionBackgroundService> logger,
        TimeSpan? interval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromHours(1); // Default 1 hour
    }

    /// <summary>
    /// Executes the background service, performing periodic document ingestion at configured intervals.
    /// Continues processing even if individual documents fail to ensure maximum data availability.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformIngestionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ingestion");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Ingestion Background Service stopped");
    }

    private async Task PerformIngestionAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var documentLoaders = scope.ServiceProvider.GetServices<IDocumentLoader>();
        var parser = scope.ServiceProvider.GetRequiredService<IBankingDocumentParser>();
        var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        var llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();

        var ingestionJob = new IngestionJob
        {
            Id = Guid.NewGuid().ToString(),
            Source = "Scheduled",
            Status = IngestionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation($"Starting ingestion job {ingestionJob.Id}");

        try
        {
            var documentsProcessed = 0;

            foreach (var loader in documentLoaders)
            {
                _logger.LogInformation($"Processing documents from {loader.GetType().Name}");

                var documents = await loader.LoadDocumentsAsync(cancellationToken);

                foreach (var document in documents)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Parse document
                        var parsedDocument = await parser.ParseDocumentAsync(document, cancellationToken);

                        // Chunk document
                        var chunks = await chunkingService.ChunkDocumentAsync(parsedDocument.Content, cancellationToken: cancellationToken);

                        // Process each chunk
                        foreach (var textChunk in chunks)
                        {
                            // Generate embedding
                            var embedding = await llmClient.GenerateEmbeddingAsync(textChunk.Content, cancellationToken);

                            // Store in vector database
                            var chunk = new DocumentChunk
                            {
                                SourceId = document.Id,
                                Content = textChunk.Content,
                                Embedding = embedding,
                                Metadata = parsedDocument.Metadata,
                                CreatedAt = DateTime.UtcNow
                            };

                            await vectorStore.StoreEmbeddingAsync(chunk, cancellationToken);
                        }

                        documentsProcessed++;
                        _logger.LogDebug($"Processed document {document.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing document {document.Id}");
                    }
                }
            }

            ingestionJob.Status = IngestionStatus.Completed;
            ingestionJob.DocumentsProcessed = documentsProcessed;
            ingestionJob.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation($"Ingestion job {ingestionJob.Id} completed. Documents processed: {documentsProcessed}");
        }
        catch (Exception ex)
        {
            ingestionJob.Status = IngestionStatus.Failed;
            ingestionJob.ErrorMessage = ex.Message;
            ingestionJob.CompletedAt = DateTime.UtcNow;

            _logger.LogError(ex, $"Ingestion job {ingestionJob.Id} failed");
            throw;
        }
    }
}