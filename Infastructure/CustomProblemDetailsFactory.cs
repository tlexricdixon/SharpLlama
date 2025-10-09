#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;      // Requires FrameworkReference Microsoft.AspNetCore.App
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using SharpLlama.Contracts;
using System.Diagnostics;

namespace SharpLlama.Infrastructure;

public sealed class CustomProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly ApiBehaviorOptions _options;
    private readonly ILoggerManager _logger;

    private const string CorrelationIdItemKey = "X-Correlation-ID";

    public CustomProblemDetailsFactory(IOptions<ApiBehaviorOptions> options, ILoggerManager logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = statusCode is >= 100 and <= 599 ? statusCode.Value : StatusCodes.Status500InternalServerError;

        var resolvedType = type ?? (_options.ClientErrorMapping.TryGetValue(status, out var mapping)
            ? mapping.Link
            : "about:blank");

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ResolveTitle(status, title),
            Type = resolvedType,
            Detail = detail,
            Instance = instance ?? httpContext.Request.Path
        };

        Enrich(problem, httpContext);
        return problem;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(modelStateDictionary);

        var status = statusCode is >= 100 and <= 599 ? statusCode.Value : StatusCodes.Status400BadRequest;

        var resolvedType = type ?? (_options.ClientErrorMapping.TryGetValue(status, out var mapping)
            ? mapping.Link
            : "about:blank");

        var validation = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = status,
            Title = ResolveTitle(status, title),
            Type = resolvedType,
            Detail = detail,
            Instance = instance ?? httpContext.Request.Path
        };

        Enrich(validation, httpContext);
        return validation;
    }

    private string ResolveTitle(int status, string? provided)
    {
        if (!string.IsNullOrWhiteSpace(provided))
            return provided;

        if (_options.ClientErrorMapping.TryGetValue(status, out var mapping) &&
            !string.IsNullOrWhiteSpace(mapping.Title))
            return mapping.Title;

        return GetDefaultTitle(status);
    }

    private void Enrich(ProblemDetails problemDetails, HttpContext context)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        if (context.Items.TryGetValue(CorrelationIdItemKey, out var cid) &&
            cid is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        if (problemDetails.Status is >= 500)
            _logger.LogError($"ProblemDetails {problemDetails.Status} {problemDetails.Title} traceId={traceId}");
    }

    private static string GetDefaultTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        499 => "Canceled",
        StatusCodes.Status500InternalServerError => "Server Error",
        _ => "Error"
    };
}