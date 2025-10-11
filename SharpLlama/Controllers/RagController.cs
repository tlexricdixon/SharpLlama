using LLama.Common;
using Microsoft.AspNetCore.Mvc;
using SharpLlama.Contracts;
using SharpLlama.Entities;
using System.Diagnostics;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/rag")]
public class RagController(IRagChatService ragChatService, IKragStore ragStore, ILoggerManager logger) : ControllerBase
{
    private readonly IRagChatService _ragChatService = ragChatService;
    private readonly IKragStore _ragStore = ragStore;
    private readonly ILoggerManager _logger = logger;

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest input, CancellationToken ct)
    {
        try
        {
            if (input?.Messages == null || input.Messages.Count == 0)
                return BadRequest(new { message = "No messages provided." });

            var history = new ChatHistory();
            foreach (var m in input.Messages)
                history.AddMessage(Enum.TryParse<AuthorRole>(m.Role, true, out var role) ? role : AuthorRole.User, m.Content ?? "");

            var sw = Stopwatch.StartNew();
            var response = await _ragChatService.SendWithRagAsync(history,null, ct);
            _logger.LogInfo($"RagController.Chat completed in {sw.ElapsedMilliseconds}ms.");
            return Ok(new { response, elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            _logger.LogError($"RagController.Chat error: {ex.Message}");
            return Problem(statusCode: 500, title: "ServerError", detail: "Chat request failed.");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchMemory([FromQuery] string query, [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query is required." });

        try
        {
            var results = await _ragStore.SearchAsync(query, topK: limit);
            return Ok(new { count = results.Count(), results });
        }
        catch (Exception ex)
        {
            _logger.LogError($"RagController.SearchMemory error: {ex.Message}");
            return Problem(statusCode: 500, title: "ServerError", detail: "Search failed.");
        }
    }
}
