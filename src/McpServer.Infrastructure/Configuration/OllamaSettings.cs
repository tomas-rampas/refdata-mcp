namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Ollama LLM service integration.
/// Defines connection parameters, model selection, and retry policies for local LLM inference.
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ModelName { get; set; } = "phi3.5";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}