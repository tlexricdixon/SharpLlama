using Contracts;
using Microsoft.AspNetCore.Mvc;

namespace SharpLlama.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MetricsController : ControllerBase
{
    private readonly IChatMetrics _metrics;
    private readonly IChatServicePool _servicePool;
    private readonly ILLamaWeightManager _weightManager;
    private readonly ILoggerManager _logger;

    public MetricsController(IChatMetrics metrics, IChatServicePool servicePool, ILLamaWeightManager weightManager, ILoggerManager logger)
    {
        _metrics = metrics;
        _servicePool = servicePool;
        _weightManager = weightManager;
        _logger = logger;
    }

    [HttpGet("chat")]
    public ActionResult<ChatMetricsSnapshot> GetChatMetrics()
    {
        return Ok(_metrics.GetSnapshot());
    }

    [HttpGet("pool")]
    public ActionResult<object> GetPoolMetrics()
    {
        return Ok(new
        {
            AvailableStatefulServices = _servicePool.AvailableStatefulCount,
            AvailableStatelessServices = _servicePool.AvailableStatelessCount,
            LoadedWeights = _weightManager.GetLoadedWeightsCount()
        });
    }

    [HttpGet("health")]
    public ActionResult<object> GetHealthStatus()
    {
        var metrics = _metrics.GetSnapshot();
        var isHealthy = metrics.TotalRequests == 0 || (metrics.TotalErrors / (double)metrics.TotalRequests) < 0.1; // Less than 10% error rate

        return Ok(new
        {
            Status = isHealthy ? "Healthy" : "Degraded",
            TotalRequests = metrics.TotalRequests,
            ErrorRate = metrics.TotalRequests > 0 ? (metrics.TotalErrors / (double)metrics.TotalRequests) * 100 : 0,
            AverageResponseTimeMs = metrics.AverageResponseTimeMs,
            CacheHitRate = (metrics.CacheHits + metrics.CacheMisses) > 0 ? (metrics.CacheHits / (double)(metrics.CacheHits + metrics.CacheMisses)) * 100 : 0
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(ChatMetricsSnapshot), StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(_metrics.GetSnapshot());
}