using SharpLlama.Contracts;
using System.Net.Mime;
using System.Text.Json;

namespace SharpLlama.Middleware;

public sealed class ProblemDetailsMiddleware(RequestDelegate next, ILoggerManager logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILoggerManager _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499; // Client Closed Request (convention)
                context.Response.ContentType = MediaTypeNames.Application.Json;
                var payload = JsonSerializer.Serialize(new
                {
                    type = "about:blank",
                    title = "Canceled",
                    status = 499,
                    detail = "Request canceled by client.",
                    traceId = context.TraceIdentifier
                });
                await context.Response.WriteAsync(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = MediaTypeNames.Application.Json;
                var payload = JsonSerializer.Serialize(new
                {
                    type = "about:blank",
                    title = "ServerError",
                    status = 500,
                    detail = "Unexpected server error.",
                    traceId = context.TraceIdentifier
                });
                await context.Response.WriteAsync(payload);
            }
        }
    }
}

public static class ProblemDetailsMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalProblemDetails(this IApplicationBuilder app)
        => app.UseMiddleware<SharpLlama.Middleware.ProblemDetailsMiddleware>();
}
