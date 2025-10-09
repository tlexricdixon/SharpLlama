using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpLlama.Contracts;

namespace SharpLlama.Controllers;

/// <summary>
/// Provides endpoints to warm-up heavy model resources (weights/context) so first real user request is faster.
/// </summary>
[ApiController]
[Route("api/warmup")]
[Authorize] // Warmup is privileged
public class WarmupController : ControllerBase
{
    private readonly ILLamaWeightManager _weights;
    private readonly IConfiguration _config;
    private readonly ILoggerManager _logger;

    public WarmupController(ILLamaWeightManager weights, IConfiguration config, ILoggerManager logger)
    {
        _weights = weights;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Triggers loading of model weights if not already loaded.
    /// </summary>
    [HttpPost]
    public ActionResult Warmup()
    {
        var modelPath = _config["ModelPath"];
        if (string.IsNullOrWhiteSpace(modelPath)) return BadRequest("ModelPath not configured");
        try
        {
            var ctxSize = (uint)(int.TryParse(_config["ContextSize"], out var cs) ? cs : 2048);
            var p = new LLama.Common.ModelParams(modelPath) { ContextSize = ctxSize };
            _weights.GetOrCreateWeights(modelPath, p);

            var fileName = Path.GetFileName(modelPath);
            _logger.LogInfo("Warmup triggered model load (file only): {0}", fileName);

            return Ok(new { status = "ok", modelFile = fileName, contextSize = ctxSize });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Warmup failed: {ex.Message}");
            return StatusCode(500, new { status = "error", error = ex.Message });
        }
    }
}
