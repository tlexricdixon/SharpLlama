using SharpLlama.Contracts;
using System.Diagnostics;

namespace SharpLlama.Middleware;

public sealed class RequestMetricsMiddleware(RequestDelegate next, IChatMetrics metrics, ILoggerManager logger)
{
    private readonly RequestDelegate _next = next;
    private readonly IChatMetrics _metrics = metrics;
    private readonly ILoggerManager _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var key = $"HTTP:{method}:{path}";
        try
        {
            await _next(context);
            sw.Stop();
            _metrics.IncrementRequestCount(key);
            _metrics.RecordResponseTime(key, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.IncrementRequestCount(key);
            _metrics.IncrementErrorCount(key, ex.GetType().Name);
            _logger.LogError($"RequestMetrics error {method} {path}: {ex.Message}");
            throw;
        }
    }
}

public static class RequestMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder app)
        => app.UseMiddleware<SharpLlama.Middleware.RequestMetricsMiddleware>();
}
