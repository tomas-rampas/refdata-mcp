using FluentAssertions;
using McpServer.Api.IntegrationTests.Fixtures;
using McpServer.Application.DTOs;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using McpServer.Core.Interfaces;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace McpServer.Api.IntegrationTests.Controllers;

public class IngestionControllerTests : IntegrationTestBase
{
    public IngestionControllerTests(McpServerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Start_Should_Return_Ok_When_Ingestion_Starts_Successfully()
    {
        // Arrange
        var documents = new List<Document>
        {
            new Document 
            { 
                Id = "doc1", 
                Content = "Test content", 
                SourcePath = "/test/doc1.pdf" 
            }
        };

        Factory.MockFileLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        Factory.MockJiraLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockConfluenceLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        // Act
        var response = await Client.PostAsync("/api/ingestion/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestionStatusDto>(JsonOptions);
        
        result.Should().NotBeNull();
        result!.JobId.Should().NotBeEmpty();
        result.Source.Should().Be("All Sources");
        result.Status.Should().BeOneOf("InProgress", "Completed", "CompletedWithErrors");
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Start_Should_Handle_Empty_Document_Sets()
    {
        // Arrange
        Factory.MockFileLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockJiraLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockConfluenceLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        // Act
        var response = await Client.PostAsync("/api/ingestion/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestionStatusDto>(JsonOptions);
        
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.DocumentsProcessed.Should().Be(0);
    }

    [Fact]
    public async Task Start_Should_Return_InternalServerError_When_Service_Fails()
    {
        // Arrange
        Factory.MockFileLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("File system error"));

        // Act
        var response = await Client.PostAsync("/api/ingestion/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("error");
    }

    [Fact]
    public async Task GetStatus_Should_Return_NotFound_When_Job_Does_Not_Exist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"/api/ingestion/status/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStatus_Should_Return_BadRequest_For_Invalid_Id()
    {
        // Act
        var response = await Client.GetAsync("/api/ingestion/status/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_Should_Continue_Processing_After_Document_Errors()
    {
        // Arrange
        var documents = new List<Document>
        {
            new Document { Id = "doc1", Content = "Valid content", SourcePath = "/test/doc1.pdf" },
            new Document { Id = "doc2", Content = "Error content", SourcePath = "/test/doc2.pdf" },
            new Document { Id = "doc3", Content = "Valid content", SourcePath = "/test/doc3.pdf" }
        };

        Factory.MockFileLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        Factory.MockJiraLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockLlmClient
            .SetupSequence(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f })  // doc1
            .ThrowsAsync(new InvalidOperationException("Embedding error")) // doc2
            .ReturnsAsync(new float[] { 0.3f }); // doc3

        // Act
        var response = await Client.PostAsync("/api/ingestion/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestionStatusDto>(JsonOptions);
        
        result.Should().NotBeNull();
        result!.Status.Should().Be("CompletedWithErrors");
        result.DocumentsProcessed.Should().BeLessThan(3);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Start_Should_Support_Concurrent_Requests()
    {
        // Arrange
        Factory.MockFileLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>
            {
                new Document { Id = "doc1", Content = "Content", SourcePath = "/test/doc.pdf" }
            });

        Factory.MockJiraLoader
            .Setup(x => x.LoadDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // Act - Start multiple ingestion jobs concurrently
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => Client.PostAsync("/api/ingestion/start", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        });

        var results = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<IngestionStatusDto>(JsonOptions))
        );

        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result!.JobId.Should().NotBeEmpty();
        });

        // All job IDs should be unique
        results.Select(r => r!.JobId).Should().OnlyHaveUniqueItems();
    }
}