using FluentAssertions;
using McpServer.Infrastructure.Configuration;
using McpServer.Infrastructure.LlmClients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace McpServer.Api.IntegrationTests.Infrastructure;

public class OllamaClientTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<OllamaClient>> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly OllamaClient _ollamaClient;

    public OllamaClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);

        _mockLogger = new Mock<ILogger<OllamaClient>>();

        _ollamaClient = new OllamaClient(_httpClient, "http://localhost:11434", "llama2");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Should_Return_Valid_Embedding()
    {
        // Arrange
        var text = "Test text for embedding";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        var response = new
        {
            embedding = expectedEmbedding
        };

        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _ollamaClient.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedEmbedding);

        var request = _mockHandler.CapturedRequests.FirstOrDefault();
        request.Should().NotBeNull();
        request!.RequestUri!.PathAndQuery.Should().Be("/api/embeddings");
        
        var requestContent = await request.Content!.ReadAsStringAsync();
        requestContent.Should().Contain("llama2"); // Uses same model for embeddings
        requestContent.Should().Contain(text);
    }

    [Fact]
    public async Task GenerateResponseAsync_Should_Return_Valid_Response()
    {
        // Arrange
        var query = "What is the policy for wire transfers?";
        var context = "Wire transfers require approval from senior management.";
        var expectedResponse = "Based on the policy, wire transfers require approval from senior management.";

        var response = new
        {
            response = expectedResponse,
            done = true
        };

        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _ollamaClient.GenerateResponseAsync(query, context);

        // Assert
        result.Should().Be(expectedResponse);

        var request = _mockHandler.CapturedRequests.FirstOrDefault();
        request.Should().NotBeNull();
        request!.RequestUri!.PathAndQuery.Should().Be("/api/generate");
        
        var requestContent = await request.Content!.ReadAsStringAsync();
        requestContent.Should().Contain("llama2");
        requestContent.Should().Contain(query);
        requestContent.Should().Contain(context);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Should_Handle_Empty_Input()
    {
        // Arrange
        var emptyText = "";
        var response = new { embedding = new float[] { } };
        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _ollamaClient.GenerateEmbeddingAsync(emptyText);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Should_Handle_Service_Errors()
    {
        // Arrange
        var text = "Test text";
        _mockHandler.SetupResponse(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable");

        // Act
        var act = async () => await _ollamaClient.GenerateEmbeddingAsync(text);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*503*");
        _mockHandler.CapturedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateResponseAsync_Should_Handle_Streaming_Response()
    {
        // Arrange
        var query = "Test query";
        var context = "Test context";
        
        // Simulate streaming response with multiple chunks
        var chunks = new[]
        {
            new { response = "Based on ", done = false },
            new { response = "the context, ", done = false },
            new { response = "here is the answer.", done = true }
        };

        var streamContent = string.Join("\n", chunks.Select(c => JsonSerializer.Serialize(c)));
        _mockHandler.SetupResponse(HttpStatusCode.OK, streamContent);

        // Act
        var result = await _ollamaClient.GenerateResponseAsync(query, context);

        // Assert
        result.Should().Be("Based on the context, here is the answer.");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Should_Throw_On_Non_Transient_Error()
    {
        // Arrange
        var text = "Test text";
        _mockHandler.SetupResponse(HttpStatusCode.BadRequest, "Invalid model");

        // Act
        var act = async () => await _ollamaClient.GenerateEmbeddingAsync(text);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*400*");
    }

    [Fact]
    public async Task GenerateResponseAsync_Should_Include_System_Prompt()
    {
        // Arrange
        var query = "What are the requirements?";
        var context = "Requirements document content";
        var response = new { response = "The requirements are...", done = true };

        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        await _ollamaClient.GenerateResponseAsync(query, context);

        // Assert
        var request = _mockHandler.CapturedRequests.FirstOrDefault();
        var requestContent = await request!.Content!.ReadAsStringAsync();
        var requestJson = JsonDocument.Parse(requestContent);
        
        // The prompt should contain both the query and context
        var prompt = requestJson.RootElement.GetProperty("prompt").GetString();
        prompt.Should().NotBeNull();
        prompt.Should().Contain(query);
        prompt.Should().Contain(context);
    }

    [Fact]
    public async Task Operations_Should_Respect_CancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHandler.SetupResponse(HttpStatusCode.OK, "{}");

        // Act & Assert - GenerateEmbeddingAsync
        var act1 = async () => await _ollamaClient.GenerateEmbeddingAsync("test", cts.Token);
        await act1.Should().ThrowAsync<OperationCanceledException>();

        // Act & Assert - GenerateResponseAsync
        var act2 = async () => await _ollamaClient.GenerateResponseAsync("query", "context", cts.Token);
        await act2.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Should_Complete_Successfully()
    {
        // Arrange
        var text = "Performance test";
        var response = new { embedding = new float[] { 0.1f, 0.2f, 0.3f } };
        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _ollamaClient.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f });
    }

    [Fact]
    public async Task GenerateResponseAsync_Should_Handle_Large_Context()
    {
        // Arrange
        var query = "Summarize the document";
        var largeContext = string.Join("\n", Enumerable.Repeat("This is a long document paragraph. ", 1000));
        var response = new { response = "Summary of the large document.", done = true };

        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _ollamaClient.GenerateResponseAsync(query, largeContext);

        // Assert
        result.Should().Be("Summary of the large document.");
        
        var request = _mockHandler.CapturedRequests.FirstOrDefault();
        var requestContent = await request!.Content!.ReadAsStringAsync();
        requestContent.Length.Should().BeGreaterThan(10000); // Verify large context was sent
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode statusCode, string content)> _responses = new();
        public List<HttpRequestMessage> CapturedRequests { get; } = new();

        public void SetupResponse(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue((statusCode, content));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            // Clone the request to capture it (including content)
            var capturedRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content != null)
            {
                var contentString = await request.Content.ReadAsStringAsync(cancellationToken);
                capturedRequest.Content = new StringContent(contentString, Encoding.UTF8, "application/json");
            }
            CapturedRequests.Add(capturedRequest);

            cancellationToken.ThrowIfCancellationRequested();

            if (_responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("No response configured")
                };
            }

            var (statusCode, content) = _responses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }

    }
}