using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using McpServer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace McpServer.Application.Tests.Services;

public class IngestionServiceTests
{
    private readonly Mock<IDocumentLoader> _mockFileLoader;
    private readonly Mock<IDocumentLoader> _mockJiraLoader;
    private readonly Mock<IBankingDocumentParser> _mockDocumentParser;
    private readonly Mock<IChunkingService> _mockChunkingService;
    private readonly Mock<ILlmClient> _mockLlmClient;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILogger<IngestionService>> _mockLogger;
    private readonly IngestionService _ingestionService;

    public IngestionServiceTests()
    {
        _mockFileLoader = new Mock<IDocumentLoader>();
        _mockJiraLoader = new Mock<IDocumentLoader>();
        _mockDocumentParser = new Mock<IBankingDocumentParser>();
        _mockChunkingService = new Mock<IChunkingService>();
        _mockLlmClient = new Mock<ILlmClient>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<IngestionService>>();

        var documentLoaders = new[] { _mockFileLoader.Object, _mockJiraLoader.Object };

        _ingestionService = new IngestionService(
            documentLoaders,
            _mockDocumentParser.Object,
            _mockChunkingService.Object,
            _mockLlmClient.Object,
            _mockVectorStore.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_DocumentLoaders_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            null!,
            _mockDocumentParser.Object,
            _mockChunkingService.Object,
            _mockLlmClient.Object,
            _mockVectorStore.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("documentLoaders");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_DocumentParser_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            new[] { _mockFileLoader.Object },
            null!,
            _mockChunkingService.Object,
            _mockLlmClient.Object,
            _mockVectorStore.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("documentParser");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_ChunkingService_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            new[] { _mockFileLoader.Object },
            _mockDocumentParser.Object,
            null!,
            _mockLlmClient.Object,
            _mockVectorStore.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chunkingService");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_LlmClient_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            new[] { _mockFileLoader.Object },
            _mockDocumentParser.Object,
            _mockChunkingService.Object,
            null!,
            _mockVectorStore.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("llmClient");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_VectorStore_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            new[] { _mockFileLoader.Object },
            _mockDocumentParser.Object,
            _mockChunkingService.Object,
            _mockLlmClient.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("vectorStore");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_Logger_Is_Null()
    {
        // Arrange & Act
        var act = () => new IngestionService(
            new[] { _mockFileLoader.Object },
            _mockDocumentParser.Object,
            _mockChunkingService.Object,
            _mockLlmClient.Object,
            _mockVectorStore.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Process_Documents_From_All_Loaders_Successfully()
    {
        // Arrange
        var fileDocuments = new List<Document>
        {
            new Document { Id = "file1", Content = "File content 1", SourcePath = "/path/file1.pdf" },
            new Document { Id = "file2", Content = "File content 2", SourcePath = "/path/file2.docx" }
        };

        var jiraDocuments = new List<Document>
        {
            new Document { Id = "jira1", Content = "Jira content 1", SourcePath = "JIRA-123" }
        };

        var parsedContent = "Parsed content";
        var extractedMetadata = new DocumentMetadata
        {
            ["Department"] = "Treasury",
            ["DocumentType"] = DocumentType.Policy
        };

        var chunks = new List<TextChunk>
        {
            new TextChunk { Content = "Chunk 1", StartIndex = 0, EndIndex = 100 },
            new TextChunk { Content = "Chunk 2", StartIndex = 80, EndIndex = 180 }
        };

        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDocuments);

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jiraDocuments);

        _mockDocumentParser.Setup(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((parsedContent, extractedMetadata));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.StoreEmbeddingAsync(It.IsAny<DocumentChunk>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored");

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Source.Should().Be("All Sources");
        result.Status.Should().Be(IngestionStatus.Completed);
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.DocumentsProcessed.Should().Be(3); // 2 files + 1 jira
        result.ErrorMessage.Should().BeNull();

        // Verify all documents were processed
        _mockDocumentParser.Verify(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockChunkingService.Verify(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockLlmClient.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(6)); // 2 chunks * 3 docs
        _mockVectorStore.Verify(x => x.StoreEmbeddingAsync(It.IsAny<DocumentChunk>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Handle_Empty_Document_Lists()
    {
        // Arrange
        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(IngestionStatus.Completed);
        result.DocumentsProcessed.Should().Be(0);
        result.ErrorMessage.Should().BeNull();

        // Verify no processing occurred
        _mockDocumentParser.Verify(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Continue_Processing_After_Document_Error()
    {
        // Arrange
        var documents = new List<Document>
        {
            new Document { Id = "doc1", Content = "Content 1", SourcePath = "/path/1" },
            new Document { Id = "doc2", Content = "Content 2", SourcePath = "/path/2" },
            new Document { Id = "doc3", Content = "Content 3", SourcePath = "/path/3" }
        };

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        // Make the second document fail during parsing
        _mockDocumentParser.SetupSequence(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Parsed 1", new DocumentMetadata()))
            .ThrowsAsync(new InvalidOperationException("Parse error"))
            .ReturnsAsync(("Parsed 3", new DocumentMetadata()));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunk> { new TextChunk { Content = "Chunk" } });

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        result.Status.Should().Be(IngestionStatus.CompletedWithErrors);
        result.DocumentsProcessed.Should().Be(2); // Only doc1 and doc3
        result.ErrorMessage.Should().Contain("Error processing document doc2: Parse error");

        // Verify processing continued after error
        _mockDocumentParser.Verify(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Handle_Loader_Failure()
    {
        // Arrange
        var fileDocuments = new List<Document>
        {
            new Document { Id = "doc1", Content = "Content 1", SourcePath = "/path/1" }
        };

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDocuments);

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Jira API error"));

        _mockDocumentParser.Setup(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Parsed", new DocumentMetadata()));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunk> { new TextChunk { Content = "Chunk" } });

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        result.Status.Should().Be(IngestionStatus.CompletedWithErrors);
        result.DocumentsProcessed.Should().Be(1); // Only from file loader
        result.ErrorMessage.Should().Contain("Jira API error");
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Handle_Complete_Failure()
    {
        // Arrange
        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Critical failure"));

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        result.Status.Should().Be(IngestionStatus.CompletedWithErrors);
        result.DocumentsProcessed.Should().Be(0);
        result.ErrorMessage.Should().Contain("Critical failure");
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Properly_Merge_Metadata()
    {
        // Arrange
        var document = new Document
        {
            Id = "doc1",
            Content = "Content",
            SourcePath = "/path/doc.pdf",
            Metadata = new Dictionary<string, object>
            {
                { "OriginalKey", "OriginalValue" },
                { "Department", "IT" } // This should be overwritten
            }
        };

        var extractedMetadata = new DocumentMetadata
        {
            ["Department"] = "Treasury", // Should overwrite
            ["NewKey"] = "NewValue" // Should be added
        };

        DocumentChunk capturedChunk = null!;

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { document });

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockDocumentParser.Setup(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Parsed", extractedMetadata));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TextChunk { Content = "Chunk" } });

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        _mockVectorStore.Setup(x => x.StoreEmbeddingAsync(It.IsAny<DocumentChunk>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentChunk, CancellationToken>((chunk, ct) => capturedChunk = chunk)
            .ReturnsAsync("stored");

        // Act
        await _ingestionService.StartIngestionAsync();

        // Assert
        capturedChunk.Should().NotBeNull();
        capturedChunk.Metadata.Should().ContainKey("OriginalKey").WhoseValue.Should().Be("OriginalValue");
        capturedChunk.Metadata.Should().ContainKey("Department").WhoseValue.Should().Be("Treasury"); // Overwritten
        capturedChunk.Metadata.Should().ContainKey("NewKey").WhoseValue.Should().Be("NewValue"); // Added
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Set_Proper_DocumentChunk_Properties()
    {
        // Arrange
        var document = new Document
        {
            Id = "source123",
            Content = "Content",
            SourcePath = "/path/doc.pdf"
        };

        var chunks = new List<TextChunk>
        {
            new TextChunk { Content = "First chunk", StartIndex = 0, EndIndex = 100 },
            new TextChunk { Content = "Second chunk", StartIndex = 80, EndIndex = 180 }
        };

        var capturedChunks = new List<DocumentChunk>();

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { document });

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockDocumentParser.Setup(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Parsed", new DocumentMetadata()));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _mockVectorStore.Setup(x => x.StoreEmbeddingAsync(It.IsAny<DocumentChunk>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentChunk, CancellationToken>((chunk, ct) => capturedChunks.Add(chunk))
            .ReturnsAsync("stored");

        // Act
        await _ingestionService.StartIngestionAsync();

        // Assert
        capturedChunks.Should().HaveCount(2);
        capturedChunks.Should().AllSatisfy(c => c.Id.Should().NotBeEmpty());
        capturedChunks.Should().AllSatisfy(c => c.SourceId.Should().Be("source123"));
        capturedChunks[0].Content.Should().Be("First chunk");
        capturedChunks[1].Content.Should().Be("Second chunk");
        capturedChunks.Should().AllSatisfy(c => c.Embedding.Should().NotBeNull());
        capturedChunks.Should().AllSatisfy(c => c.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Respect_CancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.FromCanceled<IEnumerable<Document>>(ct));

        // Act
        var act = async () => await _ingestionService.StartIngestionAsync(cts.Token);

        // Assert
        var result = await act.Should().NotThrowAsync(); // Service handles cancellation gracefully
        result.Subject.Status.Should().BeOneOf(IngestionStatus.Failed, IngestionStatus.CompletedWithErrors);
        result.Subject.ErrorMessage.Should().Contain("canceled");
    }

    [Fact]
    public async Task StartIngestionAsync_Should_Log_Progress_Information()
    {
        // Arrange
        var documents = new List<Document>
        {
            new Document { Id = "doc1", Content = "Content", SourcePath = "/path/1" }
        };

        _mockFileLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        _mockJiraLoader.Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockDocumentParser.Setup(x => x.ParseDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Parsed", new DocumentMetadata()));

        _mockChunkingService.Setup(x => x.ChunkDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TextChunk { Content = "Chunk" } });

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act
        var result = await _ingestionService.StartIngestionAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Starting ingestion job {result.Id}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing documents from")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully processed document doc1 into 1 chunks")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Ingestion job {result.Id} completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}