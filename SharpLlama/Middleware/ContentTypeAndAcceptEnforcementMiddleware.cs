using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace SharpLlama.Middleware;

public sealed class ContentTypeAndAcceptEnforcementMiddleware(RequestDelegate next, ProblemDetailsFactory problemFactory, ILogger<ContentTypeAndAcceptEnforcementMiddleware> logger)
{
    private static readonly HashSet<string> JsonMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/json; charset=utf-8"
    };

    private readonly RequestDelegate _next = next;
    private readonly ProblemDetailsFactory _problemFactory = problemFactory;
    private readonly ILogger<ContentTypeAndAcceptEnforcementMiddleware> _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path;

        // Skip health, metrics, root, GET static/openapi
        if (path.StartsWithSegments("/metrics") ||
            path.Equals("/") ||
            path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        // Enforce Accept for API endpoints
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.Headers.TryGetValue("Accept", out var acceptValues))
            {
                if (!AcceptAllowsJson(acceptValues))
                {
                    _logger.LogWarning("Rejecting request without acceptable JSON media type. Accept={Accept}", acceptValues.ToString());
                    await WriteProblem(context, StatusCodes.Status406NotAcceptable, "NotAcceptable", "Accept header must allow 'application/json'.");
                    return;
                }
            }
            else
            {
                context.Request.Headers["Accept"] = "*/*";
            }
            // If no Accept header: treat as '*/*' (typical browser/fetch) -> allowed
        }

        if (RequiresBodySemanticCheck(context.Request.Method))
        {
            // Enforce Content-Type
            if (!context.Request.Headers.TryGetValue("Content-Type", out var ct) ||
                !IsJsonContentType(ct))
            {
                _logger.LogWarning("Rejecting request with invalid or missing Content-Type. Provided={CT}", ct.ToString());
                await WriteProblem(context, StatusCodes.Status415UnsupportedMediaType, "UnsupportedMediaType", "Content-Type must be 'application/json'.");
                return;
            }

            // Optional fast-fail for large bodies (in addition to Kestrel limit)
            if (context.Request.Headers.TryGetValue("Content-Length", out var clVal) &&
                long.TryParse(clVal, out var contentLength))
            {
                var max = context.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetValue<long?>("RequestLimits:MaxRequestBodyBytes") ?? 256 * 1024;
                if (contentLength > max)
                {
                    _logger.LogWarning("Rejecting request exceeding configured MaxRequestBodyBytes. Length={Len} Limit={Lim}", contentLength, max);
                    await WriteProblem(context, StatusCodes.Status413PayloadTooLarge, "PayloadTooLarge", $"Request body exceeds {max} bytes.");
                    return;
                }
            }
        }

        if (path.StartsWithSegments("/api/StatelessChat", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Path.Value?.Contains("stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            await _next(context);
            return;
        }

        _logger.LogDebug("Path={Path} Accept={Accept} ContentType={CT}", path, context.Request.Headers["Accept"].ToString(), context.Request.ContentType);

        await _next(context);
    }

    private bool IsJsonContentType(StringValues values)
    {
        foreach (var v in values)
        {
            var trimmed = v.Split(';', 2)[0].Trim();
            if (trimmed.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool AcceptAllowsJson(StringValues acceptValues)
    {
        foreach (var v in acceptValues.SelectMany(a => a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var media = v.Split(';', 2)[0].Trim();
            if (media is "*/*" or "application/*" ||
                media.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                media.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase)) // allow SSE responses
                return true;
        }
        return false;
    }

    private static bool RequiresBodySemanticCheck(string method)
        => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

    private async Task WriteProblem(HttpContext ctx, int status, string title, string detail)
    {
        if (ctx.Response.HasStarted) return;
        var problem = _problemFactory.CreateProblemDetails(ctx, statusCode: status, title: title, detail: detail);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(problem);
    }
}

public static class ContentTypeAndAcceptEnforcementExtensions
{
    public static IApplicationBuilder UseContentTypeAndAcceptEnforcement(this IApplicationBuilder app)
        => app.UseMiddleware<ContentTypeAndAcceptEnforcementMiddleware>();
}