using Microsoft.AspNetCore.Mvc;
using McpServer.Core.Interfaces;
using System.Diagnostics;

namespace McpServer.Api.Controllers;

/// <summary>
/// API controller for health checks and system status monitoring.
/// Provides endpoints to verify the health of the application and its dependencies.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Initializes a new instance of the HealthController.
    /// </summary>
    /// <param name="vectorStore">Vector store service for health checks</param>
    /// <param name="llmClient">LLM client for health checks</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public HealthController(
        IVectorStore vectorStore,
        ILlmClient llmClient,
        ILogger<HealthController> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Basic health check endpoint.
    /// </summary>
    /// <returns>A simple health status</returns>
    /// <response code="200">The service is healthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    public ActionResult<HealthStatus> GetHealth()
    {
        return Ok(new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// Detailed health check including all dependencies.
    /// </summary>
    /// <returns>Detailed health status of all components</returns>
    /// <response code="200">All services are healthy</response>
    /// <response code="503">One or more services are unhealthy</response>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DetailedHealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DetailedHealthStatus>> GetDetailedHealth()
    {
        var sw = Stopwatch.StartNew();
        var healthChecks = new Dictionary<string, ComponentHealth>();

        // Check API health
        healthChecks["api"] = new ComponentHealth
        {
            Status = "Healthy",
            ResponseTime = 0
        };

        // Check Vector Store (MongoDB)
        var vectorStoreHealth = await CheckVectorStoreHealthAsync();
        healthChecks["vectorStore"] = vectorStoreHealth;

        // Check LLM Client (Ollama)
        var llmHealth = await CheckLlmHealthAsync();
        healthChecks["llmClient"] = llmHealth;

        sw.Stop();

        var overallHealthy = healthChecks.Values.All(h => h.Status == "Healthy");
        var status = new DetailedHealthStatus
        {
            Status = overallHealthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            TotalResponseTime = sw.ElapsedMilliseconds,
            Components = healthChecks
        };

        if (!overallHealthy)
        {
            _logger.LogWarning("Health check failed. Unhealthy components: {Components}",
                string.Join(", ", healthChecks.Where(h => h.Value.Status != "Healthy").Select(h => h.Key)));
            
            return StatusCode(StatusCodes.Status503ServiceUnavailable, status);
        }

        return Ok(status);
    }

    /// <summary>
    /// Checks the health of the vector store connection.
    /// </summary>
    private async Task<ComponentHealth> CheckVectorStoreHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Try to perform a simple search to verify connectivity
            var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _vectorStore.SearchSimilarAsync(testEmbedding, 1, 0.0f);
            
            sw.Stop();
            return new ComponentHealth
            {
                Status = "Healthy",
                ResponseTime = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Vector store health check failed");
            return new ComponentHealth
            {
                Status = "Unhealthy",
                ResponseTime = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks the health of the LLM client connection.
    /// </summary>
    private async Task<ComponentHealth> CheckLlmHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Try to generate a simple embedding to verify connectivity
            await _llmClient.GenerateEmbeddingAsync("health check");
            
            sw.Stop();
            return new ComponentHealth
            {
                Status = "Healthy",
                ResponseTime = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "LLM client health check failed");
            return new ComponentHealth
            {
                Status = "Unhealthy",
                ResponseTime = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Liveness probe for container orchestration.
    /// </summary>
    /// <returns>200 OK if the service is alive</returns>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetLiveness()
    {
        return Ok();
    }

    /// <summary>
    /// Readiness probe for container orchestration.
    /// </summary>
    /// <returns>200 OK if the service is ready to accept requests</returns>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> GetReadiness()
    {
        // Check if critical services are available
        try
        {
            var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _vectorStore.SearchSimilarAsync(testEmbedding, 1, 0.0f);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}

/// <summary>
/// Basic health status response.
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the health check.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Application version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Detailed health status including all components.
/// </summary>
public class DetailedHealthStatus : HealthStatus
{
    /// <summary>
    /// Total response time for all health checks in milliseconds.
    /// </summary>
    public long TotalResponseTime { get; set; }

    /// <summary>
    /// Health status of individual components.
    /// </summary>
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
}

/// <summary>
/// Health status of an individual component.
/// </summary>
public class ComponentHealth
{
    /// <summary>
    /// Component health status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long ResponseTime { get; set; }

    /// <summary>
    /// Error message if the component is unhealthy.
    /// </summary>
    public string? Error { get; set; }
}