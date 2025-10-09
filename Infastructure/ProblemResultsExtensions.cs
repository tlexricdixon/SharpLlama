using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SharpLlama.Infrastructure;

public static class ProblemResultsExtensions
{
    public static IActionResult ValidationProblem(this ControllerBase c, string detail, int statusCode = StatusCodes.Status400BadRequest)
        => c.Problem(statusCode: statusCode, title: "ValidationError", detail: detail);

    public static IActionResult CanceledProblem(this ControllerBase c)
        => c.Problem(statusCode: 499, title: "Canceled", detail: "Request canceled by client.");

    public static IActionResult ServerErrorProblem(this ControllerBase c, string? detail = null)
        => c.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "ServerError", detail: detail ?? "Unexpected server error.");
}
