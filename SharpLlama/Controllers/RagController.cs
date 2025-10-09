using LLama.Common;
using Microsoft.AspNetCore.Mvc;
using SharpLlama.Contracts;
using SharpLlama.Entities;
using SharpLlama.Infrastructure;
using System.Diagnostics;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController(IRagChatService ragChatService, IMemoryService memoryService, ILoggerManager logger) : ControllerBase
{
    private readonly IRagChatService _ragChatService = ragChatService;
    private readonly IMemoryService _memoryService = memoryService;
    private readonly ILoggerManager _logger = logger;

    [HttpPost("chat")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInfo($"RagController.Chat started. MessageCount={input.Messages.Count}");
            var history = new ChatHistory();
            foreach (var m in input.Messages)
            {
                if (!Enum.TryParse<AuthorRole>(m.Role, true, out var role)) continue;
                var content = (m.Content ?? string.Empty).Trim();
                if (content.Length == 0) continue;
                if (content.Length > ChatValidationConstants.MaxMessageChars)
                    content = content[..ChatValidationConstants.MaxMessageChars];
                history.AddMessage(role, content);
            }
            if (history.Messages.Count == 0)
                return this.ValidationProblem("No valid messages after validation.");
            var response = await _ragChatService.SendWithRagAsync(history, cancellationToken: ct);
            _logger.LogDebug($"RagController.Chat success in {sw.ElapsedMilliseconds}ms len={response.Length}");
            return Ok(new { response, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (OperationCanceledException)
        { return this.CanceledProblem(); }
        catch (Exception ex)
        { _logger.LogError($"RagController.Chat error: {ex.Message}"); return this.ServerErrorProblem(); }
    }
}
