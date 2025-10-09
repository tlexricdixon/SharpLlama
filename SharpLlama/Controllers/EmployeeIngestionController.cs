using ChatService;
using Microsoft.AspNetCore.Mvc;
using SharpLlama.ChatService;

namespace SharpLlama.Controllers;

/// <summary>
/// API controller responsible for triggering ingestion of all <see cref="Employee"/> entities
/// into the retrieval augmented generation (RAG) memory store via <see cref="EmployeeRagIngestionService"/>.
/// </summary>
/// <remarks>
/// Primary usage is to bulk (re)ingest employee profile data so it becomes searchable
/// for downstream chat / semantic retrieval scenarios.
/// 
/// Security / throttling considerations (not implemented here) should be applied in production,
/// because this endpoint performs a potentially expensive full data scan.
/// </remarks>
/// <param name="svc">
/// The ingestion service (resolved via dependency injection) that performs the actual
/// transformation of employees into vectorized / chunked documents stored in the memory service.
/// </param>
/// <seealso cref="EmployeeRagIngestionService"/>
[ApiController]
[Route("api/ingest/employees")]
public class EmployeeIngestionController(EmployeeRagIngestionService svc) : ControllerBase
{
    private readonly EmployeeRagIngestionService _svc = svc;

    /// <summary>
    /// Performs a full ingestion pass of all employees into the memory store.
    /// </summary>
    /// <param name="ct">Cancellation token to cooperatively cancel the ingestion early.</param>
    /// <returns>
    /// 200 OK with a JSON payload: { "imported": &lt;number_of_employees_processed&gt; }.
    /// </returns>
    /// <response code="200">Returns the number of employee records ingested.</response>
    /// <response code="500">An unhandled error occurred during ingestion.</response>
    /// <example>
    /// curl -X POST https://localhost:5001/api/ingest/employees
    /// </example>
    [HttpPost]
    public async Task<IActionResult> IngestAll(CancellationToken ct)
    {
        var count = await _svc.IngestAllAsync(ct);
        return Ok(new { imported = count });
    }
}