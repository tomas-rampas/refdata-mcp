using McpServer.Application.DTOs;
using McpServer.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.Api.Controllers;

/// <summary>
/// API controller for handling chat queries to the banking reference data system.
/// Provides endpoints for natural language querying of policies, procedures, and reference data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly RagService _ragService;
    private readonly ILogger<ChatController> _logger;

    /// <summary>
    /// Initializes a new instance of the ChatController.
    /// </summary>
    /// <param name="ragService">The RAG service for processing queries</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public ChatController(RagService ragService, ILogger<ChatController> logger)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a natural language query and returns relevant information from the banking reference data.
    /// </summary>
    /// <param name="request">The chat request containing the user's query</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A chat response with the answer and source documents</returns>
    /// <response code="200">Returns the chat response with answer and sources</response>
    /// <response code="400">If the request is invalid or query is empty</response>
    /// <response code="500">If an internal error occurs during processing</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponseDto>> ProcessQuery(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Query cannot be empty",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            _logger.LogInformation("Processing chat query from user {UserId}", request.UserId ?? "anonymous");

            var chatResult = await _ragService.ProcessQueryAsync(
                request.Query,
                request.UserId,
                cancellationToken);

            var response = new ChatResponseDto
            {
                Id = chatResult.Id,
                Query = chatResult.Query,
                Response = chatResult.Response,
                Timestamp = chatResult.Timestamp,
                Sources = MapSourceDocuments(chatResult)
            };

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Chat query processing was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat query");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while processing your query. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Maps the document chunks from the chat result to source document DTOs.
    /// </summary>
    private List<SourceDocumentDto> MapSourceDocuments(Core.Entities.ChatRequest chatResult)
    {
        // In a real implementation, we would retrieve the actual chunks by their IDs
        // For now, we'll return a placeholder implementation
        var sources = new List<SourceDocumentDto>();

        if (chatResult.RelevantChunks != null && chatResult.RelevantChunks.Any())
        {
            // This would normally fetch the actual chunks from the vector store
            // For now, we'll create placeholder sources
            foreach (var chunkId in chatResult.RelevantChunks.Take(3))
            {
                sources.Add(new SourceDocumentDto
                {
                    Title = "Banking Policy Document",
                    Department = "Risk Management",
                    DocumentType = "Policy",
                    SourcePath = $"documents/{chunkId}",
                    Excerpt = "Relevant content excerpt would appear here..."
                });
            }
        }

        return sources;
    }
}