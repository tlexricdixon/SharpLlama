using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Infrastructure;
using System.IO;

namespace SharpLlama.Controllers;

[ApiController]
[Route("api/config")]
[Authorize] // protect internal configuration
public sealed class ConfigController : ControllerBase
{
    private readonly ModelOptions _model;
    private readonly ChatServiceOptions _chat;
    private readonly OtlpOptions _otlp;

    public ConfigController(
        IOptionsMonitor<ModelOptions> model,
        IOptionsMonitor<ChatServiceOptions> chat,
        IOptionsMonitor<OtlpOptions> otlp)
    {
        _model = model.CurrentValue;
        _chat  = chat.CurrentValue;
        _otlp  = otlp.CurrentValue;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        // Only expose non-sensitive / summarized data
        var pathExists = !string.IsNullOrWhiteSpace(_model.ModelPath) && System.IO.File.Exists(_model.ModelPath);
        var fileName = string.IsNullOrWhiteSpace(_model.ModelPath) ? "" : Path.GetFileName(_model.ModelPath);

        return Ok(new
        {
            model = new
            {
                fileName,
                pathExists,
                _model.ContextSize
            },
            chat = new
            {
                _chat.RequestTimeoutSeconds,
                _chat.MemorySearchTimeoutSeconds
            },
            otlp = new
            {
                _otlp.MetricsEndpoint,
                _otlp.TracesEndpoint
            }
        });
    }
}