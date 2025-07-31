using System.Text;
using System.Text.Json;
using McpServer.Core.Interfaces;
using Polly;

namespace McpServer.Infrastructure.LlmClients;

/// <summary>
/// Provides integration with Ollama LLM service for embedding generation and text completion.
/// Implements retry logic for resilience and supports local model inference.
/// </summary>
public class OllamaClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _modelName;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaClient(HttpClient httpClient, string baseUrl = "http://localhost:11434", string modelName = "phi3.5")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl;
        _modelName = modelName;

        // Configure retry policy
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount} after {timespan} seconds");
                });
    }

    /// <inheritdoc cref="ILlmClient.GenerateEmbeddingAsync"/>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _modelName,
            prompt = text
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsync($"{_baseUrl}/api/embeddings", content, cancellationToken));

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        
        var embedding = doc.RootElement
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();

        return embedding;
    }

    /// <inheritdoc cref="ILlmClient.GenerateResponseAsync"/>
    public async Task<string> GenerateResponseAsync(string prompt, string context, CancellationToken cancellationToken = default)
    {
        var fullPrompt = string.IsNullOrEmpty(context) 
            ? prompt 
            : $"Context:\n{context}\n\nQuestion: {prompt}\n\nAnswer:";
            
        var request = new
        {
            model = _modelName,
            prompt = fullPrompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9,
                max_tokens = 1000
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cancellationToken));

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }
}