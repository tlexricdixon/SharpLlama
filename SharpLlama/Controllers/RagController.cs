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

    // NEW: list document ids present in AI_ContextChunks
    [HttpGet("memory/documents")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentIds(CancellationToken ct)
    {
        try
        {
            var ids = await _memoryService.GetDocumentIdsAsync().ConfigureAwait(false);
            return Ok(new { count = ids.Count(), ids });
        }
        catch (OperationCanceledException) { return this.CanceledProblem(); }
        catch (Exception ex) { _logger.LogError($"GetDocumentIds error: {ex.Message}"); return this.ServerErrorProblem(); }
    }

    // NEW: search chunks with a free-text query (pulls text like your Adatum sample)
    [HttpGet("memory/search")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchMemory([FromQuery] string query, [FromQuery] int limit = 5, [FromQuery] double minRelevance = 0.4, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return this.ValidationProblem("Query is required.");
        try
        {
            var results = await _memoryService.SearchAsync(query, limit, minRelevance).ConfigureAwait(false);
            var payload = results.Select(c => new
            {
                source = c.SourceName,
                partitions = (c.Partitions ?? Enumerable.Empty<Microsoft.KernelMemory.Citation.Partition>())
                    .Select(p => new { number = p.PartitionNumber, relevance = p.Relevance, text = p.Text })
            }).ToList();

            return Ok(new
            {
                query,
                limit,
                minRelevance,
                count = payload.Count,
                results = payload
            });
        }
        catch (OperationCanceledException) { return this.CanceledProblem(); }
        catch (Exception ex) { _logger.LogError($"SearchMemory error: {ex.Message}"); return this.ServerErrorProblem(); }
    }
}
