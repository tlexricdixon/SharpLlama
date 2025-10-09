using LLama.Common;
using SharpLlama.Contracts;

namespace SharpLlama.HostedServices;

/// <summary>
/// Background hosted service that optionally pre-loads model weights on application startup.
/// Controlled via configuration key: ChatService:Warmup:Enabled (bool) and ChatService:Warmup:DelaySeconds (int, optional).
/// </summary>
public sealed class ModelWarmupHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILLamaWeightManager _weightManager;
    private readonly ILoggerManager _logger;

    public ModelWarmupHostedService(IConfiguration configuration, ILLamaWeightManager weightManager, ILoggerManager logger)
    {
        _configuration = configuration;
        _weightManager = weightManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!bool.TryParse(_configuration["ChatService:Warmup:Enabled"], out var enabled) || !enabled)
        {
            _logger.LogDebug("Model warmup disabled (ChatService:Warmup:Enabled=false)");
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = int.TryParse(_configuration["ChatService:Warmup:DelaySeconds"], out var d) ? d : 0;
                if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);

                var modelPath = _configuration["ModelPath"];
                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    _logger.LogWarning("Warmup skipped: ModelPath missing");
                    return;
                }
                var ctxSize = (uint)(int.TryParse(_configuration["ContextSize"], out var cs) ? cs : 2048);
                var p = new ModelParams(modelPath) { ContextSize = ctxSize };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _weightManager.GetOrCreateWeights(modelPath, p);
                sw.Stop();
                _logger.LogInfo($"Model warmup completed in {sw.ElapsedMilliseconds} ms model={modelPath} ctx={ctxSize}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Model warmup failed: {ex.Message}");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
