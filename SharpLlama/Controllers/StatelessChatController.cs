using Contracts;
using Entities;
using LLama.Common;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Infrastructure;

namespace SharpLlama.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatelessChatController(ILoggerManager logger, IStatelessChatService statelessChatService) : ControllerBase
{
    private readonly ILoggerManager _logger = logger;
    private readonly IStatelessChatService _statelessChatService = statelessChatService;

    [HttpPost("History")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendHistory([FromBody] HistoryInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInfo($"SendHistory started. Count={input.Messages.Count}");
            var history = BuildHistory(input);
            if (history.Messages.Count == 0)
                return this.ValidationProblem("No valid messages after validation.");
            var response = await _statelessChatService.SendAsync(history, ct);
            _logger.LogDebug($"SendHistory ok in {sw.ElapsedMilliseconds}ms len={response.Length}");
            return Ok(new { response, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (OperationCanceledException)
        { return this.CanceledProblem(); }
        catch (Exception ex)
        { _logger.LogError($"SendHistory error: {ex.Message}"); return this.ServerErrorProblem(); }
    }

    [HttpPost("SendEnhanced")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendEnhancedMessage([FromBody] SendMessageInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInfo($"SendEnhancedMessage started. PromptLength={input.Text.Length}");
            var history = new ChatHistory();
            history.AddMessage(AuthorRole.User, input.Text);
            var response = await _statelessChatService.SendAsync(history, ct);
            _logger.LogDebug($"SendEnhancedMessage ok in {sw.ElapsedMilliseconds}ms len={response.Length}");
            return Ok(new { response, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (OperationCanceledException)
        { return this.CanceledProblem(); }
        catch (Exception ex)
        { _logger.LogError($"SendEnhancedMessage error: {ex.Message}"); return this.ServerErrorProblem(); }
    }

    private ChatHistory BuildHistory(HistoryInput input)
    {
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
        return history;
    }
}
