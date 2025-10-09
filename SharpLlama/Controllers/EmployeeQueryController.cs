using ChatService;
using Contracts;
using Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeQueryController(
    StructuredEmployeeQueryService structuredService,
    IRagChatService ragChatService,
    ILoggerManager logger) : ControllerBase
{
    private readonly StructuredEmployeeQueryService _structuredService = structuredService;
    private readonly IRagChatService? _ragChatService = ragChatService;
    private readonly ILoggerManager _logger = logger;

    [HttpGet("structured")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    public async Task<IActionResult> GetStructured([FromQuery] string question, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Problem(statusCode: 400, title: "ValidationError", detail: "Question is required.");
        if (question.Length > ChatValidationConstants.MaxMessageChars)
            return Problem(statusCode: 400, title: "ValidationError", detail: $"Question exceeds {ChatValidationConstants.MaxMessageChars} characters.");

        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInfo($"EmployeeQueryController.GetStructured started. QuestionLength={question.Length}");
            var result = await _structuredService.TryAnswerAsync(question, ct);
            if (string.IsNullOrEmpty(result))
                return NotFound(new { message = "No structured answer available." });
            return Ok(new { source = "structured", question, result, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (OperationCanceledException)
        { return Problem(statusCode: 499, title: "Canceled", detail: "Request canceled by client."); }
        catch (Exception ex)
        { _logger.LogError($"EmployeeQueryController.GetStructured error: {ex.Message}"); return Problem(statusCode: 500, title: "ServerError", detail: "Unexpected server error."); }
    }

    [Authorize] // Protect hybrid POST (chat / fallback path)
    [HttpPost("hybrid")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PostHybrid([FromBody] EmployeeQueryRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
            return Problem(statusCode: 400, title: "ValidationError", detail: "Question is required.");
        if (request.Question.Length > ChatValidationConstants.MaxMessageChars)
            return Problem(statusCode: 400, title: "ValidationError", detail: $"Question exceeds {ChatValidationConstants.MaxMessageChars} characters.");

        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInfo($"EmployeeQueryController.PostHybrid started. QuestionLength={request.Question.Length} Fallback={request.IncludeFallback}");
            var structured = await _structuredService.TryAnswerAsync(request.Question, ct);
            if (!string.IsNullOrEmpty(structured))
                return Ok(new { source = "structured", request.Question, result = structured, elapsedMs = sw.ElapsedMilliseconds });
            if (!request.IncludeFallback)
                return NotFound(new { message = "No structured answer available and fallback disabled." });
            if (_ragChatService == null)
                return Problem(statusCode: 503, title: "Unavailable", detail: "Fallback RAG service not configured.");
            var history = new LLama.Common.ChatHistory();
            history.AddMessage(LLama.Common.AuthorRole.User, request.Question);
            var ragAnswer = await _ragChatService.SendAsync(history, ct);
            return Ok(new { source = "rag-fallback", request.Question, result = ragAnswer, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (OperationCanceledException)
        { return Problem(statusCode: 499, title: "Canceled", detail: "Request canceled by client."); }
        catch (Exception ex)
        { _logger.LogError($"EmployeeQueryController.PostHybrid error: {ex.Message}"); return Problem(statusCode: 500, title: "ServerError", detail: "Unexpected server error."); }
    }

    public record EmployeeQueryRequest(string Question, bool IncludeFallback = true);

    private sealed class NullLoggerManager : ILoggerManager
    { public void LogDebug(string message) { } public void LogError(string message) { } public void LogInfo(string message, params object[] args) { } public void LogWarning(string message) { } }
}