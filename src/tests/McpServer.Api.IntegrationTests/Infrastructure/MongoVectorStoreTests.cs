using FluentAssertions;
using McpServer.Core.Entities;
using McpServer.Infrastructure.Configuration;
using McpServer.Infrastructure.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;
using Xunit;

namespace McpServer.Api.IntegrationTests.Infrastructure;

public class MongoVectorStoreTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .WithPortBinding(27017, true)
        .Build();

    private MongoVectorStore _vectorStore = null!;
    private readonly Mock<ILogger<MongoVectorStore>> _mockLogger = new();

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        var connectionString = _mongoContainer.GetConnectionString();
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("McpServerTestDb");
        
        _vectorStore = new MongoVectorStore(database);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    [Fact]
    public async Task StoreEmbeddingAsync_Should_Store_And_Retrieve_DocumentChunk()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            SourceId = "test-source",
            Content = "This is test content for MongoDB integration",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
            Metadata = new DocumentMetadata
            {
                Title = "Test Document",
                Department = "IT",
                DocumentType = Core.Enums.DocumentType.Policy,
                EffectiveDate = DateTime.UtcNow,
                Version = "1.0"
            },
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var storedId = await _vectorStore.StoreEmbeddingAsync(chunk);

        // Assert
        storedId.Should().NotBeEmpty();
        storedId.Should().Be(chunk.Id);
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task SearchSimilarAsync_Should_Find_Similar_Documents()
    {
        // Arrange
        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Id = "chunk1",
                SourceId = "source1",
                Content = "Wire transfer policy for international transactions",
                Embedding = new float[] { 0.9f, 0.1f, 0.0f, 0.0f, 0.0f },
                Metadata = new DocumentMetadata { Title = "Wire Transfer Policy" }
            },
            new DocumentChunk
            {
                Id = "chunk2",
                SourceId = "source2",
                Content = "Compliance requirements for KYC procedures",
                Embedding = new float[] { 0.0f, 0.9f, 0.1f, 0.0f, 0.0f },
                Metadata = new DocumentMetadata { Title = "KYC Procedures" }
            },
            new DocumentChunk
            {
                Id = "chunk3",
                SourceId = "source1",
                Content = "International wire transfer limits and approvals",
                Embedding = new float[] { 0.8f, 0.2f, 0.0f, 0.0f, 0.0f },
                Metadata = new DocumentMetadata { Title = "Wire Transfer Limits" }
            }
        };

        foreach (var chunk in chunks)
        {
            await _vectorStore.StoreEmbeddingAsync(chunk);
        }

        var queryEmbedding = new float[] { 0.85f, 0.15f, 0.0f, 0.0f, 0.0f };

        // Act
        var results = await _vectorStore.SearchSimilarAsync(queryEmbedding, topK: 2);

        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(2);
        resultList[0].Id.Should().BeOneOf("chunk1", "chunk3"); // Most similar to wire transfer docs
        resultList[1].Id.Should().BeOneOf("chunk1", "chunk3");
        resultList.Should().NotContain(c => c.Id == "chunk2"); // KYC doc should not be in top 2
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task SearchSimilarAsync_Should_Respect_MinimumSimilarityScore()
    {
        // Arrange
        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Id = "similar",
                Content = "Highly relevant content",
                Embedding = new float[] { 0.95f, 0.05f, 0.0f },
                Metadata = new DocumentMetadata()
            },
            new DocumentChunk
            {
                Id = "dissimilar",
                Content = "Unrelated content",
                Embedding = new float[] { 0.0f, 0.0f, 1.0f },
                Metadata = new DocumentMetadata()
            }
        };

        foreach (var chunk in chunks)
        {
            await _vectorStore.StoreEmbeddingAsync(chunk);
        }

        var queryEmbedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var results = await _vectorStore.SearchSimilarAsync(
            queryEmbedding, 
            topK: 10, 
            minimumSimilarityScore: 0.5f);

        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].Id.Should().Be("similar");
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task SearchSimilarAsync_Should_Return_Empty_When_No_Documents_Match()
    {
        // Arrange
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var results = await _vectorStore.SearchSimilarAsync(queryEmbedding, topK: 5);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task StoreEmbeddingAsync_Should_Handle_Large_Embeddings()
    {
        // Arrange
        var largeEmbedding = new float[1536]; // Common embedding size
        for (int i = 0; i < largeEmbedding.Length; i++)
        {
            largeEmbedding[i] = (float)(i % 100) / 100f;
        }

        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Document with large embedding",
            Embedding = largeEmbedding,
            Metadata = new DocumentMetadata()
        };

        // Act
        var storedId = await _vectorStore.StoreEmbeddingAsync(chunk);

        // Assert
        storedId.Should().NotBeEmpty();
        
        // Verify we can search with large embeddings
        var results = await _vectorStore.SearchSimilarAsync(largeEmbedding, topK: 1);
        results.Should().HaveCount(1);
        results.First().Id.Should().Be(chunk.Id);
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task SearchSimilarAsync_Should_Include_All_Metadata_In_Results()
    {
        // Arrange
        var metadata = new DocumentMetadata
        {
            Title = "Complete Document",
            Department = "Risk Management",
            DocumentType = Core.Enums.DocumentType.Procedure,
            EffectiveDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Version = "2.1",
            ["CustomField"] = "CustomValue"
        };

        var chunk = new DocumentChunk
        {
            Id = "metadata-test",
            Content = "Content with complete metadata",
            Embedding = new float[] { 1.0f, 0.0f, 0.0f },
            Metadata = metadata
        };

        await _vectorStore.StoreEmbeddingAsync(chunk);

        // Act
        var results = await _vectorStore.SearchSimilarAsync(
            new float[] { 1.0f, 0.0f, 0.0f }, 
            topK: 1);

        // Assert
        var result = results.First();
        result.Metadata.Title.Should().Be("Complete Document");
        result.Metadata.Department.Should().Be("Risk Management");
        result.Metadata.DocumentType.Should().Be(Core.Enums.DocumentType.Procedure);
        result.Metadata.EffectiveDate.Should().Be(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.Metadata.Version.Should().Be("2.1");
        result.Metadata["CustomField"].Should().Be("CustomValue");
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task StoreEmbeddingAsync_Should_Update_Existing_Document()
    {
        // Arrange
        var chunkId = "update-test";
        var originalChunk = new DocumentChunk
        {
            Id = chunkId,
            Content = "Original content",
            Embedding = new float[] { 0.1f, 0.2f },
            Metadata = new DocumentMetadata { Title = "Original" }
        };

        var updatedChunk = new DocumentChunk
        {
            Id = chunkId,
            Content = "Updated content",
            Embedding = new float[] { 0.3f, 0.4f },
            Metadata = new DocumentMetadata { Title = "Updated" }
        };

        // Act
        await _vectorStore.StoreEmbeddingAsync(originalChunk);
        await _vectorStore.StoreEmbeddingAsync(updatedChunk);

        // Search for the updated embedding
        var results = await _vectorStore.SearchSimilarAsync(
            new float[] { 0.3f, 0.4f }, 
            topK: 10);

        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(1); // Should only have one document with this ID
        resultList[0].Content.Should().Be("Updated content");
        resultList[0].Metadata.Title.Should().Be("Updated");
    }

    [Fact(Skip = "Requires MongoDB Atlas with vector search capability")]
    public async Task Operations_Should_Be_Thread_Safe()
    {
        // Arrange
        var tasks = new List<Task>();
        var chunkIds = new List<string>();

        // Act - Perform concurrent operations
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var chunk = new DocumentChunk
                {
                    Id = $"concurrent-{index}",
                    Content = $"Concurrent content {index}",
                    Embedding = new float[] { index / 10f, 1 - (index / 10f) },
                    Metadata = new DocumentMetadata()
                };

                var storedId = await _vectorStore.StoreEmbeddingAsync(chunk);
                lock (chunkIds)
                {
                    chunkIds.Add(storedId);
                }

                // Also perform searches
                await _vectorStore.SearchSimilarAsync(
                    new float[] { 0.5f, 0.5f }, 
                    topK: 5);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        chunkIds.Should().HaveCount(10);
        chunkIds.Should().OnlyHaveUniqueItems();
        
        // Verify all documents were stored
        var searchResults = await _vectorStore.SearchSimilarAsync(
            new float[] { 0.5f, 0.5f }, 
            topK: 20);
        
        searchResults.Count().Should().Be(10);
    }
}