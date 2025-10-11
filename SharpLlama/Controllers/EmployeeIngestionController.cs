using Microsoft.AspNetCore.Mvc;
using SharpLlama.ChatService;
using SharpLlama.Contracts;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/ingest/employees")]
public class EmployeeIngestionController(EmployeeRagIngestionService svc, ILoggerManager logger) : ControllerBase
{
    private readonly EmployeeRagIngestionService _svc = svc;
    private readonly ILoggerManager _logger = logger;

    [HttpPost]
    public async Task<IActionResult> IngestAll(CancellationToken ct)
    {
        try
        {
            _logger.LogInfo("Starting full employee ingestion...");
            var count = await _svc.IngestAllAsync(ct);
            _logger.LogInfo($"Employee ingestion complete: {count} records processed.");
            return Ok(new { imported = count });
        }
        catch (OperationCanceledException)
        {
            return Problem(statusCode: 499, title: "Canceled", detail: "Request canceled by client.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"EmployeeIngestionController error: {ex.Message}");
            return Problem(statusCode: 500, title: "ServerError", detail: "Error during ingestion.");
        }
    }
}
