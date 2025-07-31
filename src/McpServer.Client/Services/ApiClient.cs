using System.Net.Http.Json;
using System.Text.Json;
using McpServer.Application.DTOs;

namespace McpServer.Client.Services;

/// <summary>
/// HTTP client for API operations
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<ChatResponseDto> SendChatQueryAsync(ChatRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/chat", request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ChatResponseDto>(content, _jsonOptions) 
            ?? throw new InvalidOperationException("Failed to deserialize chat response");
    }

    /// <inheritdoc />
    public async Task<string> StartIngestionAsync()
    {
        var response = await _httpClient.PostAsync("api/ingestion/start", null);
        response.EnsureSuccessStatusCode();
        
        var jobId = await response.Content.ReadAsStringAsync();
        return jobId.Trim('"'); // Remove quotes from the response
    }

    /// <inheritdoc />
    public async Task<IngestionStatusDto?> GetIngestionStatusAsync()
    {
        var response = await _httpClient.GetAsync("api/ingestion/status");
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IngestionStatusDto>(content, _jsonOptions);
    }
}