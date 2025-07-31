using System;
using FluentAssertions;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using Xunit;

namespace McpServer.Core.Tests.Entities;

/// <summary>
/// Unit tests for the DocumentChunk entity
/// </summary>
public class DocumentChunkTests
{
    [Fact]
    public void DocumentChunk_Constructor_Should_Initialize_Properties()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var sourceId = "test-source";
        var content = "Test content";
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        var metadata = new DocumentMetadata
        {
            Title = "Test Title",
            Department = "Test Department",
            DocumentType = DocumentType.Policy,
            EffectiveDate = DateTime.UtcNow,
            Version = "1.0"
        };

        // Act
        var chunk = new DocumentChunk
        {
            Id = id,
            SourceId = sourceId,
            Content = content,
            Embedding = embedding,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        chunk.Id.Should().Be(id);
        chunk.SourceId.Should().Be(sourceId);
        chunk.Content.Should().Be(content);
        chunk.Embedding.Should().BeEquivalentTo(embedding);
        chunk.Metadata.Should().BeSameAs(metadata);
        chunk.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DocumentChunk_Should_Handle_Null_Embedding()
    {
        // Arrange & Act
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            SourceId = "test-source",
            Content = "Test content",
            Embedding = null,
            Metadata = new DocumentMetadata()
        };

        // Assert
        chunk.Embedding.Should().BeNull();
    }

    [Fact]
    public void DocumentChunk_Should_Handle_Empty_Content()
    {
        // Arrange & Act
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            SourceId = "test-source",
            Content = string.Empty,
            Embedding = new[] { 0.1f },
            Metadata = new DocumentMetadata()
        };

        // Assert
        chunk.Content.Should().BeEmpty();
    }

    [Fact]
    public void DocumentChunk_Should_Handle_Null_Metadata()
    {
        // Arrange & Act
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            SourceId = "test-source",
            Content = "Test content",
            Embedding = new[] { 0.1f },
            Metadata = null
        };

        // Assert
        chunk.Metadata.Should().BeNull();
    }
}