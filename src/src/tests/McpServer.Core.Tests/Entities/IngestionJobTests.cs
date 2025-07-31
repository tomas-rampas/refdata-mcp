using System;
using FluentAssertions;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using Xunit;

namespace McpServer.Core.Tests.Entities;

/// <summary>
/// Unit tests for the IngestionJob entity
/// </summary>
public class IngestionJobTests
{
    [Fact]
    public void IngestionJob_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var job = new IngestionJob();

        // Assert
        job.Id.Should().BeEmpty();
        job.Source.Should().BeEmpty();
        job.Status.Should().Be(IngestionStatus.Pending); // Default enum value
        job.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        job.CompletedAt.Should().BeNull();
        job.DocumentsProcessed.Should().Be(0);
        job.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void IngestionJob_Should_Set_All_Properties_Correctly()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var source = "Jira";
        var status = IngestionStatus.InProgress;
        var startedAt = DateTime.UtcNow;
        var completedAt = DateTime.UtcNow.AddMinutes(5);
        var documentsProcessed = 42;
        var errorMessage = "Test error";

        // Act
        var job = new IngestionJob
        {
            Id = id,
            Source = source,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DocumentsProcessed = documentsProcessed,
            ErrorMessage = errorMessage
        };

        // Assert
        job.Id.Should().Be(id);
        job.Source.Should().Be(source);
        job.Status.Should().Be(status);
        job.StartedAt.Should().Be(startedAt);
        job.CompletedAt.Should().Be(completedAt);
        job.DocumentsProcessed.Should().Be(documentsProcessed);
        job.ErrorMessage.Should().Be(errorMessage);
    }

    [Theory]
    [InlineData(IngestionStatus.Pending)]
    [InlineData(IngestionStatus.InProgress)]
    [InlineData(IngestionStatus.Completed)]
    [InlineData(IngestionStatus.Failed)]
    public void IngestionJob_Should_Accept_All_Status_Values(IngestionStatus status)
    {
        // Arrange & Act
        var job = new IngestionJob { Status = status };

        // Assert
        job.Status.Should().Be(status);
    }

    [Fact]
    public void IngestionJob_Completed_Should_Have_CompletedAt()
    {
        // Arrange
        var job = new IngestionJob
        {
            Status = IngestionStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow
        };

        // Assert
        job.CompletedAt.Should().NotBeNull();
        job.CompletedAt.Should().BeAfter(job.StartedAt);
    }

    [Fact]
    public void IngestionJob_Failed_Should_Have_ErrorMessage()
    {
        // Arrange
        var errorMessage = "Connection timeout to source system";
        var job = new IngestionJob
        {
            Status = IngestionStatus.Failed,
            ErrorMessage = errorMessage
        };

        // Assert
        job.ErrorMessage.Should().NotBeNull();
        job.ErrorMessage.Should().Be(errorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void IngestionJob_Should_Accept_Various_DocumentsProcessed_Values(int count)
    {
        // Arrange & Act
        var job = new IngestionJob { DocumentsProcessed = count };

        // Assert
        job.DocumentsProcessed.Should().Be(count);
    }

    [Theory]
    [InlineData("Local Files")]
    [InlineData("Jira")]
    [InlineData("Confluence")]
    [InlineData("SharePoint")]
    public void IngestionJob_Should_Accept_Various_Source_Values(string source)
    {
        // Arrange & Act
        var job = new IngestionJob { Source = source };

        // Assert
        job.Source.Should().Be(source);
    }
}