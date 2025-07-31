using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Core.Entities;
using McpServer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace McpServer.Application.Tests.Services;

public class RagServiceTests
{
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILlmClient> _mockLlmClient;
    private readonly Mock<ILogger<RagService>> _mockLogger;
    private readonly RagService _ragService;

    public RagServiceTests()
    {
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLlmClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<RagService>>();
        _ragService = new RagService(_mockVectorStore.Object, _mockLlmClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_VectorStore_Is_Null()
    {
        // Arrange & Act
        var act = () => new RagService(null!, _mockLlmClient.Object, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("vectorStore");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_LlmClient_Is_Null()
    {
        // Arrange & Act
        var act = () => new RagService(_mockVectorStore.Object, null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("llmClient");
    }

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_When_Logger_Is_Null()
    {
        // Arrange & Act
        var act = () => new RagService(_mockVectorStore.Object, _mockLlmClient.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Throw_ArgumentException_When_Query_Is_Empty()
    {
        // Arrange
        var emptyQuery = "";

        // Act
        var act = async () => await _ragService.ProcessQueryAsync(emptyQuery);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("query")
            .WithMessage("Query cannot be empty*");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Throw_ArgumentException_When_Query_Is_Whitespace()
    {
        // Arrange
        var whitespaceQuery = "   ";

        // Act
        var act = async () => await _ragService.ProcessQueryAsync(whitespaceQuery);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("query")
            .WithMessage("Query cannot be empty*");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Process_Valid_Query_Successfully()
    {
        // Arrange
        var query = "What is the policy for wire transfers?";
        var userId = "user123";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var relevantChunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Id = "chunk1",
                Content = "Wire transfer policy content",
                Metadata = new DocumentMetadata
                {
                    ["Title"] = "Wire Transfer Policy",
                    ["Department"] = "Treasury",
                    ["DocumentType"] = "Policy",
                    ["EffectiveDate"] = "2024-01-01"
                }
            },
            new DocumentChunk
            {
                Id = "chunk2",
                Content = "Additional wire transfer guidelines",
                Metadata = new DocumentMetadata
                {
                    ["Title"] = "Wire Transfer Guidelines",
                    ["Department"] = "Operations"
                }
            }
        };
        var response = "Based on the policy documents, wire transfers require...";

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(
                embedding, 
                5, // MaxRelevantChunks
                0.7f, // MinimumSimilarityScore
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(relevantChunks);

        _mockLlmClient.Setup(x => x.GenerateResponseAsync(
                query,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _ragService.ProcessQueryAsync(query, userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Query.Should().Be(query);
        result.Response.Should().Be(response);
        result.RelevantChunks.Should().HaveCount(2);
        result.RelevantChunks.Should().BeEquivalentTo(relevantChunks);
        result.UserId.Should().Be(userId);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify all dependencies were called
        _mockLlmClient.Verify(x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _mockVectorStore.Verify(x => x.SearchSimilarAsync(embedding, 5, 0.7f, It.IsAny<CancellationToken>()), Times.Once);
        _mockLlmClient.Verify(x => x.GenerateResponseAsync(query, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Handle_No_Relevant_Chunks()
    {
        // Arrange
        var query = "What is the policy for something not in the database?";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var emptyChunks = new List<DocumentChunk>();
        var response = "I couldn't find any relevant information.";

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(
                embedding,
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyChunks);

        _mockLlmClient.Setup(x => x.GenerateResponseAsync(
                query,
                It.Is<string>(ctx => ctx.Contains("No relevant information found")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _ragService.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.RelevantChunks.Should().BeEmpty();
        result.Response.Should().Be(response);
        result.UserId.Should().BeEmpty(); // Default when not provided
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Build_Context_With_All_Metadata()
    {
        // Arrange
        var query = "Test query";
        var embedding = new float[] { 0.1f };
        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Content = "Content with all metadata",
                Metadata = new DocumentMetadata
                {
                    ["Title"] = "Complete Document",
                    ["Department"] = "Risk Management",
                    ["DocumentType"] = "Procedure",
                    ["EffectiveDate"] = "2024-06-01"
                }
            }
        };

        string capturedContext = null!;

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockLlmClient.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((q, context, ct) => capturedContext = context)
            .ReturnsAsync("Response");

        // Act
        await _ragService.ProcessQueryAsync(query);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext.Should().Contain("Document: Complete Document");
        capturedContext.Should().Contain("Department: Risk Management");
        capturedContext.Should().Contain("Type: Procedure");
        capturedContext.Should().Contain("Effective Date: 2024-06-01");
        capturedContext.Should().Contain("Content with all metadata");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Handle_Chunks_With_Missing_Metadata()
    {
        // Arrange
        var query = "Test query";
        var embedding = new float[] { 0.1f };
        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Content = "Content without metadata",
                Metadata = new DocumentMetadata()
            },
            new DocumentChunk
            {
                Content = "Content with partial metadata",
                Metadata = new DocumentMetadata
                {
                    ["Department"] = "Compliance"
                }
            }
        };

        string capturedContext = null!;

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockLlmClient.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((q, context, ct) => capturedContext = context)
            .ReturnsAsync("Response");

        // Act
        var result = await _ragService.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        capturedContext.Should().Contain("Document: Unknown");
        capturedContext.Should().Contain("Department: Compliance");
        capturedContext.Should().Contain("Content without metadata");
        capturedContext.Should().Contain("Content with partial metadata");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Propagate_Exceptions_From_LlmClient()
    {
        // Arrange
        var query = "Test query";
        var expectedException = new InvalidOperationException("LLM service unavailable");

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = async () => await _ragService.ProcessQueryAsync(query);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LLM service unavailable");

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error processing query: {query}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Propagate_Exceptions_From_VectorStore()
    {
        // Arrange
        var query = "Test query";
        var embedding = new float[] { 0.1f };
        var expectedException = new TimeoutException("Vector store timeout");

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = async () => await _ragService.ProcessQueryAsync(query);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("Vector store timeout");
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Respect_CancellationToken()
    {
        // Arrange
        var query = "Test query";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((q, ct) => Task.FromCanceled<float[]>(ct));

        // Act
        var act = async () => await _ragService.ProcessQueryAsync(query, null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessQueryAsync_Should_Log_Information_Messages()
    {
        // Arrange
        var query = "Test query";
        var embedding = new float[] { 0.1f };
        var chunks = new List<DocumentChunk> { new DocumentChunk { Content = "Test" } };

        _mockLlmClient.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockVectorStore.Setup(x => x.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockLlmClient.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        // Act
        await _ragService.ProcessQueryAsync(query);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing query: {query}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found 1 relevant chunks")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully processed query with 1 relevant chunks")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}