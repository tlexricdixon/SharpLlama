using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/rag")]
[Authorize] // protect diagnostics
public class RagDiagnosticsController : ControllerBase
{
    private readonly IRagDiagnosticsCollector _collector;
    private readonly IMemoryService _memory;

    public RagDiagnosticsController(IRagDiagnosticsCollector collector, IMemoryService memory)
    {
        _collector = collector;
        _memory = memory;
    }

    [HttpGet("diagnostics")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public IActionResult GetRecent([FromQuery] int count = 20)
    {
        if (count <= 0) count = 1;
        if (count > 100) count = 100;

        var recent = _collector.GetRecent(count);
        var shaped = recent.Select(r => new
        {
            r.RequestId,
            r.Timestamp,
            r.Query,
            LimitRequested = r.LimitRequested,
            ResultCount = r.Results.Count,
            Results = r.Results.Select(res => new
            {
                res.SourceName,
                res.MaxRelevance,
                ChunkCount = res.Chunks.Count,
                Chunks = res.Chunks.Select(c => new
                {
                    c.PartitionId,
                    c.Relevance,
                    c.TextPreview
                })
            })
        });

        return Ok(shaped);
    }

    [HttpGet("store/summary")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStoreSummary([FromQuery] bool includeIds = false, [FromQuery] int maxIds = 50)
    {
        if (maxIds < 0) maxIds = 0;
        if (maxIds > 200) maxIds = 200;

        var ids = await _memory.GetDocumentIdsAsync();
        var list = ids.ToList();
        var result = new
        {
            DocumentCount = list.Count,
            ReturnedIds = includeIds ? list.Take(maxIds).ToList() : new List<string>(),
            IncludedIdCount = includeIds ? Math.Min(maxIds, list.Count) : 0
        };
        return Ok(result);
    }
}