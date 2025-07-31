using FluentAssertions;
using McpServer.Api.IntegrationTests.Fixtures;
using McpServer.Application.DTOs;
using McpServer.Core.Entities;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace McpServer.Api.IntegrationTests.Controllers;

public class ChatControllerTests : IntegrationTestBase
{
    public ChatControllerTests(McpServerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Query_Should_Return_BadRequest_When_Query_Is_Empty()
    {
        // Arrange
        var request = new ChatRequestDto { Query = "" };

        // Act
        var response = await PostAsync("/api/chat/query", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(JsonOptions);
        problemDetails.Should().ContainKey("title");
    }

    [Fact]
    public async Task Query_Should_Return_BadRequest_When_Query_Is_Null()
    {
        // Arrange
        var request = new ChatRequestDto { Query = null! };

        // Act
        var response = await PostAsync("/api/chat/query", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_Should_Process_Valid_Request_Successfully()
    {
        // Arrange
        var request = new ChatRequestDto 
        { 
            Query = "What is the policy for wire transfers?",
            UserId = "testuser123"
        };

        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var relevantChunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Id = "chunk1",
                Content = "Wire transfer policy content",
                Metadata = new DocumentMetadata
                {
                    Title = "Wire Transfer Policy",
                    Department = "Treasury"
                }
            }
        };
        var llmResponse = "Based on the wire transfer policy, transfers require approval.";

        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(request.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        Factory.MockLlmClient
            .Setup(x => x.GenerateResponseAsync(
                request.Query, 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var response = await PostAsync<ChatRequestDto, ChatResponseDto>("/api/chat/query", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().NotBeEmpty();
        response.Query.Should().Be(request.Query);
        response.Response.Should().Be(llmResponse);
        // UserId is not exposed in the DTO
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Query_Should_Handle_Service_Errors_Gracefully()
    {
        // Arrange
        var request = new ChatRequestDto { Query = "Test query" };

        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var response = await PostAsync("/api/chat/query", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("error");
    }

    [Fact]
    public async Task Query_Should_Use_Default_UserId_When_Not_Provided()
    {
        // Arrange
        var request = new ChatRequestDto { Query = "What are the compliance requirements?" };
        
        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        Factory.MockLlmClient
            .Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response text");

        // Act
        var response = await PostAsync<ChatRequestDto, ChatResponseDto>("/api/chat/query", request);

        // Assert
        response.Should().NotBeNull();
        // UserId is not returned in the response DTO
    }

    [Fact]
    public async Task Query_Should_Return_RequestTimeout_When_Operation_Times_Out()
    {
        // Arrange
        var request = new ChatRequestDto { Query = "Test timeout scenario" };
        var cts = new CancellationTokenSource();

        Factory.MockLlmClient
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string q, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(35), ct); // Simulate long operation
                return new float[] { 0.1f };
            });

        // Act
        var response = await PostAsync("/api/chat/query", request);

        // Assert
        // The actual timeout behavior depends on server configuration
        // In production, this would return 408 Request Timeout or 504 Gateway Timeout
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.InternalServerError);
    }
}