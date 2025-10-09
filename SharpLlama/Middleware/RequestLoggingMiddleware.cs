using System.Diagnostics;
using System.Text.Json;
using Contracts;

namespace SharpLlama.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILoggerManager logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILoggerManager _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid) ? cid?.ToString() : string.Empty;
        try
        {
            await _next(context);
            sw.Stop();
            _logger.LogInfo($"HTTP {method} {path} {context.Response.StatusCode} {sw.ElapsedMilliseconds}ms cid={correlationId}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError($"HTTP {method} {path} failed {ex.GetType().Name}: {ex.Message} {sw.ElapsedMilliseconds}ms cid={correlationId}");
            throw;
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<SharpLlama.Middleware.RequestLoggingMiddleware>();
}
