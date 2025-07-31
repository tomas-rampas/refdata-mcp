using McpServer.Application.DTOs;
using McpServer.Application.Services;
using McpServer.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace McpServer.Api.Controllers;

/// <summary>
/// API controller for managing document ingestion operations.
/// Provides endpoints to start ingestion jobs and monitor their progress.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly IngestionService _ingestionService;
    private readonly ILogger<IngestionController> _logger;
    
    // In-memory storage for job tracking (in production, use a database or cache)
    private static readonly ConcurrentDictionary<string, Core.Entities.IngestionJob> _runningJobs = new();

    /// <summary>
    /// Initializes a new instance of the IngestionController.
    /// </summary>
    /// <param name="ingestionService">The ingestion service for processing documents</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public IngestionController(IngestionService ingestionService, ILogger<IngestionController> logger)
    {
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a new document ingestion job.
    /// </summary>
    /// <param name="request">The ingestion request parameters</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The status of the newly created ingestion job</returns>
    /// <response code="202">Returns the initial status of the ingestion job</response>
    /// <response code="400">If another ingestion job is already running</response>
    /// <response code="500">If an internal error occurs</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(IngestionStatusDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<IngestionStatusDto>> StartIngestion(
        [FromBody] StartIngestionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if another ingestion is already running
            var runningJob = _runningJobs.Values.FirstOrDefault(j => 
                j.Status == IngestionStatus.InProgress || j.Status == IngestionStatus.Pending);
            
            if (runningJob != null)
            {
                return Task.FromResult<ActionResult<IngestionStatusDto>>(BadRequest(new ProblemDetails
                {
                    Title = "Ingestion Already Running",
                    Detail = $"An ingestion job is already in progress (Job ID: {runningJob.Id}). Please wait for it to complete.",
                    Status = StatusCodes.Status400BadRequest
                }));
            }

            _logger.LogInformation("Starting new ingestion job for source: {Source}", 
                request?.Source ?? "All Sources");

            // Start the ingestion asynchronously
            var ingestionTask = Task.Run(async () =>
            {
                var job = await _ingestionService.StartIngestionAsync(cancellationToken);
                _runningJobs[job.Id] = job;
                return job;
            }, cancellationToken);

            // Create a placeholder job while the actual job starts
            var placeholderJob = new Core.Entities.IngestionJob
            {
                Id = Guid.NewGuid().ToString(),
                Source = request?.Source ?? "All Sources",
                Status = IngestionStatus.Pending,
                StartedAt = DateTime.UtcNow,
                DocumentsProcessed = 0
            };

            _runningJobs[placeholderJob.Id] = placeholderJob;

            // Update with actual job when it starts
            _ = ingestionTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    _runningJobs.TryRemove(placeholderJob.Id, out _);
                    _runningJobs[t.Result.Id] = t.Result;
                }
            }, TaskScheduler.Default);

            var statusDto = MapToStatusDto(placeholderJob);
            return Task.FromResult<ActionResult<IngestionStatusDto>>(Accepted(statusDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting ingestion job");
            
            return Task.FromResult<ActionResult<IngestionStatusDto>>(StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while starting the ingestion job. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            }));
        }
    }

    /// <summary>
    /// Gets the status of a specific ingestion job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the ingestion job</param>
    /// <returns>The current status of the ingestion job</returns>
    /// <response code="200">Returns the ingestion job status</response>
    /// <response code="404">If the job ID is not found</response>
    [HttpGet("status/{jobId}")]
    [ProducesResponseType(typeof(IngestionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<IngestionStatusDto> GetIngestionStatus(string jobId)
    {
        if (_runningJobs.TryGetValue(jobId, out var job))
        {
            var statusDto = MapToStatusDto(job);
            return Ok(statusDto);
        }

        return NotFound(new ProblemDetails
        {
            Title = "Job Not Found",
            Detail = $"No ingestion job found with ID: {jobId}",
            Status = StatusCodes.Status404NotFound
        });
    }

    /// <summary>
    /// Gets the status of all ingestion jobs.
    /// </summary>
    /// <param name="includeCompleted">Whether to include completed jobs in the response</param>
    /// <returns>List of all ingestion job statuses</returns>
    /// <response code="200">Returns the list of ingestion job statuses</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(IEnumerable<IngestionStatusDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<IngestionStatusDto>> GetAllIngestionStatus(
        [FromQuery] bool includeCompleted = true)
    {
        var jobs = _runningJobs.Values.AsEnumerable();
        
        if (!includeCompleted)
        {
            jobs = jobs.Where(j => 
                j.Status == IngestionStatus.InProgress || 
                j.Status == IngestionStatus.Pending);
        }

        var statuses = jobs
            .OrderByDescending(j => j.StartedAt)
            .Select(MapToStatusDto)
            .ToList();

        return Ok(statuses);
    }

    /// <summary>
    /// Maps an IngestionJob entity to an IngestionStatusDto.
    /// </summary>
    private IngestionStatusDto MapToStatusDto(Core.Entities.IngestionJob job)
    {
        var dto = new IngestionStatusDto
        {
            JobId = job.Id,
            Source = job.Source,
            Status = job.Status.ToString(),
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            DocumentsProcessed = job.DocumentsProcessed,
            ErrorMessage = job.ErrorMessage
        };

        // Calculate progress percentage if job is in progress
        if (job.Status == IngestionStatus.InProgress && job.DocumentsProcessed > 0)
        {
            // This is a simplified calculation - in production, you'd have better progress tracking
            dto.ProgressPercentage = Math.Min(100, job.DocumentsProcessed * 2); // Assumes ~50 docs total
        }
        else if (job.Status == IngestionStatus.Completed || job.Status == IngestionStatus.CompletedWithErrors)
        {
            dto.ProgressPercentage = 100;
        }
        else if (job.Status == IngestionStatus.Failed)
        {
            dto.ProgressPercentage = 0;
        }

        // Calculate estimated time remaining for in-progress jobs
        if (job.Status == IngestionStatus.InProgress && job.DocumentsProcessed > 0)
        {
            var elapsed = DateTime.UtcNow - job.StartedAt;
            var avgTimePerDoc = elapsed.TotalSeconds / job.DocumentsProcessed;
            var estimatedDocsRemaining = Math.Max(0, 50 - job.DocumentsProcessed); // Assumes ~50 docs
            var secondsRemaining = avgTimePerDoc * estimatedDocsRemaining;
            dto.EstimatedTimeRemaining = TimeSpan.FromSeconds(secondsRemaining);
        }

        return dto;
    }
}