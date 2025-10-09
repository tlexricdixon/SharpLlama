using System.Diagnostics;

namespace SharpLlama.Middleware;

public sealed class LogEnrichmentMiddleware(RequestDelegate next, ILogger<LogEnrichmentMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<LogEnrichmentMiddleware> _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var requestId = context.Request.Headers.TryGetValue("X-Request-ID", out var rid)
            ? rid.ToString()
            : Guid.NewGuid().ToString("n");

        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var cid)
            ? cid.ToString()
            : context.TraceIdentifier;

        var traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;
        var spanId = Activity.Current?.SpanId.ToString() ?? string.Empty;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
            ["SpanId"] = spanId
        }))
        {
            context.Response.Headers["X-Request-ID"] = requestId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            await _next(context);
        }
    }
}

public static class LogEnrichmentMiddlewareExtensions
{
    public static IApplicationBuilder UseLogEnrichment(this IApplicationBuilder app)
        => app.UseMiddleware<LogEnrichmentMiddleware>();
}