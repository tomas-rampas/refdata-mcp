using System;
using System.Collections.Generic;
using FluentAssertions;
using McpServer.Core.Entities;
using Xunit;

namespace McpServer.Core.Tests.Entities;

/// <summary>
/// Unit tests for the ChatRequest entity
/// </summary>
public class ChatRequestTests
{
    [Fact]
    public void ChatRequest_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var request = new ChatRequest();

        // Assert
        request.Id.Should().BeEmpty();
        request.Query.Should().BeEmpty();
        request.Response.Should().BeEmpty();
        request.RelevantChunks.Should().NotBeNull().And.BeEmpty();
        request.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.UserId.Should().BeEmpty();
    }

    [Fact]
    public void ChatRequest_Should_Set_All_Properties_Correctly()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var query = "What is the reference data policy for customer identifiers?";
        var response = "According to the reference data policy, customer identifiers must...";
        var relevantChunks = new List<DocumentChunk>
        {
            new DocumentChunk { Id = "chunk1", Content = "Customer identifier policy section 1" },
            new DocumentChunk { Id = "chunk2", Content = "Customer identifier policy section 2" }
        };
        var timestamp = DateTime.UtcNow;
        var userId = "user123";

        // Act
        var request = new ChatRequest
        {
            Id = id,
            Query = query,
            Response = response,
            RelevantChunks = relevantChunks,
            Timestamp = timestamp,
            UserId = userId
        };

        // Assert
        request.Id.Should().Be(id);
        request.Query.Should().Be(query);
        request.Response.Should().Be(response);
        request.RelevantChunks.Should().BeEquivalentTo(relevantChunks);
        request.Timestamp.Should().Be(timestamp);
        request.UserId.Should().Be(userId);
    }

    [Fact]
    public void ChatRequest_Should_Handle_Empty_RelevantChunks()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            RelevantChunks = new List<DocumentChunk>()
        };

        // Assert
        request.RelevantChunks.Should().NotBeNull();
        request.RelevantChunks.Should().BeEmpty();
    }

    [Fact]
    public void ChatRequest_Should_Handle_Null_RelevantChunks()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            RelevantChunks = null
        };

        // Assert
        request.RelevantChunks.Should().BeNull();
    }

    [Theory]
    [InlineData("What is the policy for data retention?")]
    [InlineData("Show me all reference data for currency codes")]
    [InlineData("How do I update customer reference data?")]
    [InlineData("")]
    public void ChatRequest_Should_Accept_Various_Query_Values(string query)
    {
        // Arrange & Act
        var request = new ChatRequest { Query = query };

        // Assert
        request.Query.Should().Be(query);
    }

    [Fact]
    public void ChatRequest_Should_Track_Multiple_RelevantChunks()
    {
        // Arrange
        var chunks = new List<DocumentChunk>();
        for (int i = 0; i < 10; i++)
        {
            chunks.Add(new DocumentChunk
            {
                Id = $"chunk{i}",
                Content = $"Content {i}",
                SourceId = $"source{i % 3}"
            });
        }

        // Act
        var request = new ChatRequest
        {
            RelevantChunks = chunks
        };

        // Assert
        request.RelevantChunks.Should().HaveCount(10);
        request.RelevantChunks.Should().AllSatisfy(chunk =>
        {
            chunk.Id.Should().NotBeNullOrEmpty();
            chunk.Content.Should().NotBeNullOrEmpty();
            chunk.SourceId.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public void ChatRequest_Timestamp_Should_Be_Precise()
    {
        // Arrange
        var expectedTimestamp = new DateTime(2024, 1, 15, 10, 30, 45, 123, DateTimeKind.Utc);

        // Act
        var request = new ChatRequest
        {
            Timestamp = expectedTimestamp
        };

        // Assert
        request.Timestamp.Should().Be(expectedTimestamp);
        request.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        request.Timestamp.Millisecond.Should().Be(123);
    }

    [Theory]
    [InlineData("")]
    [InlineData("anonymous")]
    [InlineData("user@bank.com")]
    [InlineData("admin123")]
    public void ChatRequest_Should_Accept_Various_UserId_Values(string userId)
    {
        // Arrange & Act
        var request = new ChatRequest { UserId = userId };

        // Assert
        request.UserId.Should().Be(userId);
    }
}