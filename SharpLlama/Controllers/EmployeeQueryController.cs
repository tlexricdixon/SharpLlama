using Microsoft.AspNetCore.Mvc;
using SharpLlama.ChatService;
using SharpLlama.Contracts;
using System.Diagnostics;
using LLama.Common;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeQueryController(
    StructuredEmployeeQueryService structuredService,
    IRagChatService ragChatService,
    ILoggerManager logger) : ControllerBase
{
    private readonly StructuredEmployeeQueryService _structuredService = structuredService;
    private readonly IRagChatService _ragChatService = ragChatService;
    private readonly ILoggerManager _logger = logger;

    [HttpGet("structured")]
    public async Task<IActionResult> GetStructured([FromQuery] string question, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { message = "Question is required." });

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _structuredService.TryAnswerAsync(question, ct);
            if (string.IsNullOrEmpty(result))
                return NotFound(new { message = "No structured answer available." });

            return Ok(new { source = "structured", question, result, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            _logger.LogError($"EmployeeQueryController.GetStructured: {ex.Message}");
            return Problem(statusCode: 500, title: "ServerError", detail: "Unexpected error.");
        }
    }

    [HttpPost("hybrid")]
    public async Task<IActionResult> PostHybrid([FromBody] EmployeeQueryRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });

        var sw = Stopwatch.StartNew();
        try
        {
            var structured = await _structuredService.TryAnswerAsync(request.Question, ct);
            if (!string.IsNullOrEmpty(structured))
                return Ok(new { source = "structured", result = structured, elapsedMs = sw.ElapsedMilliseconds });

            var history = new ChatHistory();
            history.AddMessage(AuthorRole.User, request.Question);
            var ragAnswer = await _ragChatService.SendAsync(history, ct);

            return Ok(new { source = "rag", result = ragAnswer, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            _logger.LogError($"EmployeeQueryController.PostHybrid: {ex.Message}");
            return Problem(statusCode: 500, title: "ServerError", detail: "Unexpected error.");
        }
    }

    public record EmployeeQueryRequest(string Question);
}
