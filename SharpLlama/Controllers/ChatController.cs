using Contracts;
using Entities;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Infrastructure;
using Microsoft.Extensions.Options;

namespace SharpLlama.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatefulChatController(ILoggerManager logger,
                                   IStatefulChatService statefulChatService,
                                   IOptions<ModelOptions> modelOptions) : ControllerBase
{
    private readonly ILoggerManager _logger = logger;
    private readonly IStatefulChatService _statefulChatService = statefulChatService;
    private readonly int _contextSize = modelOptions.Value.ContextSize;
    private const string ServiceType = "StatefulChatService";
    private const double MaxContextUsageFraction = 0.90;

    private (int tokens, double pct) EstimatePromptTokens(string text)
    {
        var tokens = (text.Length + 3) / 4;
        var pct = _contextSize > 0 ? (double)tokens / _contextSize : 0;
        return (tokens, pct);
    }

    private bool ExceedsLimit(int estimatedTokens) =>
        _contextSize > 0 && estimatedTokens > (_contextSize * MaxContextUsageFraction);

    [HttpPost("Send")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), 499)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (estTokens, pct) = EstimatePromptTokens(input.Text);
            ChatService.ChatOtelMetrics.RecordEstimatedPromptTokens(estTokens, ServiceType, _contextSize);
            if (ExceedsLimit(estTokens))
            {
                _logger.LogWarning($"evt=PromptRejected service={ServiceType} reason=TooLarge estTokens={estTokens} ctxSize={_contextSize} limitFraction={MaxContextUsageFraction} estPct={pct:P2}");
                return Problem(statusCode: 400,
                    title: "PromptTooLarge",
                    detail: $"Estimated prompt size {estTokens} tokens exceeds {MaxContextUsageFraction:P0} of context window ({_contextSize}). Shorten input.");
            }

            _logger.LogInfo($"evt=SendStart service={ServiceType} chars={input.Text.Length} estTokens={estTokens} estPct={pct:P2}");
            var response = await _statefulChatService.Send(input);
            _logger.LogInfo($"evt=SendComplete service={ServiceType} elapsedMs={sw.ElapsedMilliseconds} estTokens={estTokens}");
            return Ok(new { response, elapsedMs = sw.ElapsedMilliseconds, estimatedPromptTokens = estTokens });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"evt=SendCanceled service={ServiceType}");
            return this.CanceledProblem();
        }
        catch (Exception ex)
        {
            _logger.LogError($"evt=SendError service={ServiceType} errorType={ex.GetType().Name} message=\"{ex.Message}\"");
            return this.ServerErrorProblem();
        }
    }

    [HttpPost("Send/Stream")]
    public async Task SendMessageStream([FromBody] SendMessageInput input, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (!ModelState.IsValid)
        {
            await Response.WriteAsync("event: error\ndata:{\"error\":\"Validation failed.\"}\n\n", cancellationToken);
            await Response.CompleteAsync();
            return;
        }

        var (estTokens, pct) = EstimatePromptTokens(input.Text);
        ChatService.ChatOtelMetrics.RecordEstimatedPromptTokens(estTokens, ServiceType, _contextSize);
        if (ExceedsLimit(estTokens))
        {
            _logger.LogWarning($"evt=StreamPromptRejected service={ServiceType} estTokens={estTokens} ctxSize={_contextSize} limitFraction={MaxContextUsageFraction} estPct={pct:P2}");
            await Response.WriteAsync($"event: error\ndata:{{\"error\":\"PromptTooLarge\",\"detail\":\"Estimated {estTokens} tokens exceeds {MaxContextUsageFraction:P0} of context ({_contextSize}).\"}}\n\n", cancellationToken);
            await Response.CompleteAsync();
            return;
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatInterval = TimeSpan.FromSeconds(15);
        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(heartbeatInterval, linkedCts.Token);
                    if (linkedCts.Token.IsCancellationRequested) break;
                    if (!HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Response.WriteAsync(": ping\n\n");
                        await Response.Body.FlushAsync();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning($"evt=HeartbeatError service={ServiceType} message=\"{ex.Message}\"");
            }
        }, linkedCts.Token);

        try
        {
            _logger.LogInfo($"evt=StreamStart service={ServiceType} chars={input.Text.Length} estTokens={estTokens} estPct={pct:P2}");
            await foreach (var r in _statefulChatService
                .SendStream(input, linkedCts.Token)
                .WithCancellation(linkedCts.Token))
            {
                if (linkedCts.IsCancellationRequested) break;
                var safe = r.Replace("\r", "\\r").Replace("\n", "\\n");
                await Response.WriteAsync("data:" + safe + "\n\n", linkedCts.Token);
                await Response.Body.FlushAsync(linkedCts.Token);
            }

            if (!linkedCts.IsCancellationRequested && !Response.HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Response.WriteAsync("event: end\ndata:[DONE]\n\n", linkedCts.Token);
                _logger.LogInfo($"evt=StreamComplete service={ServiceType}");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"evt=StreamCanceled service={ServiceType}");
        }
        catch (Exception ex)
        {
            try { await Response.WriteAsync("event: error\ndata:{\"error\":\"Streaming failure.\"}\n\n"); } catch { }
            _logger.LogError($"evt=StreamError service={ServiceType} errorType={ex.GetType().Name} message=\"{ex.Message}\"");
        }
        finally
        {
            try
            {
                linkedCts.Cancel();
                await Task.WhenAny(heartbeatTask, Task.Delay(500));
                await Response.CompleteAsync();
            }
            catch { }
            linkedCts.Dispose();
        }
    }
}
