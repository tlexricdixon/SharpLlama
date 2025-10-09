using System.Diagnostics;
using Contracts;

namespace SharpLlama.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILoggerManager logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next = next;
    private readonly ILoggerManager _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetOrAddCorrelationId(context);
        var activity = Activity.Current;
        activity?.AddTag("correlation_id", correlationId);
        context.Items[HeaderName] = correlationId;

        // Set header as early as possible (before any body write)
        if (!context.Response.HasStarted && !context.Response.Headers.ContainsKey(HeaderName))
        {
            context.Response.Headers[HeaderName] = correlationId;
        }

        // Also register OnStarting in case another component cleared headers
        context.Response.OnStarting(state =>
        {
            var httpContext = (HttpContext)state;
            if (!httpContext.Response.Headers.ContainsKey(HeaderName))
            {
                httpContext.Response.Headers[HeaderName] = correlationId;
            }
            return Task.CompletedTask;
        }, context);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Correlation pipeline exception: {ex.Message}");
            throw;
        }
        // No attempt to set the header after response started (avoids InvalidOperationException)
    }

    private string GetOrAddCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return existing.ToString();
        var generated = Guid.NewGuid().ToString();
        _logger.LogDebug($"Generated new correlation id: {generated}");
        return generated;
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<SharpLlama.Middleware.CorrelationIdMiddleware>();
}
