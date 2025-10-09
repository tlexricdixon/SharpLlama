using LLama;
using LLama.Common;
using Microsoft.Extensions.Configuration;
using SharpLlama.Contracts;
using System.Diagnostics;
using System.Text;
using static LLama.LLamaTransforms;

namespace SharpLlama.ChatService;

public class StatelessChatService : IStatelessChatService, IDisposable
{
    private static readonly ActivitySource Activity = new("SharpLlama.Chat");
    private readonly LLamaContext _context;
    private readonly ChatSession _session;
    private readonly ILoggerManager _logger;
    private readonly IChatResponseCache _cache;
    private readonly IChatMetrics _metrics;
    private readonly int _contextSize;
    private readonly TimeSpan _requestTimeout;
    private bool _disposed = false;
    private const string ServiceType = "StatelessChatService";

    public StatelessChatService(IConfiguration configuration, ILoggerManager logger, ILLamaWeightManager weightManager, IChatResponseCache? cache = null, IChatMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? new NullChatResponseCache();
        _metrics = metrics ?? new NullChatMetrics();
        var modelPath = configuration["ModelPath"] ?? throw new InvalidOperationException("ModelPath is required in configuration");
        _contextSize = int.TryParse(configuration["ContextSize"], out var contextSize) ? contextSize : 2048;
        var timeoutSeconds = int.TryParse(configuration["ChatService:RequestTimeoutSeconds"], out var timeout) ? timeout : 120;
        _requestTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        var @params = new ModelParams(modelPath) { ContextSize = (uint)_contextSize };
        try
        {
            var weights = weightManager.GetOrCreateWeights(modelPath, @params);
            _context = new LLamaContext(weights, @params);
            var executor = new InteractiveExecutor(_context);
            _session = new ChatSession(executor)
                        .WithOutputTransform(new KeywordTextOutputStreamTransform(["User:", "Assistant:"], redundancyLength: 8))
                        .WithHistoryTransform(new HistoryTransform());
            _logger.LogInfo($"StatelessChatService initialized with context size: {_contextSize} timeout: {_requestTimeout}");
        }
        catch { _context?.Dispose(); throw; }
    }

    public async Task<string> SendAsync(ChatHistory history, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StatelessChatService));
        ArgumentNullException.ThrowIfNull(history);
        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementRequestCount(ServiceType);
        using var activity = Activity.StartActivity("chat.stateless.send", ActivityKind.Server);
        activity?.SetTag("chat.service", ServiceType);
        activity?.SetTag("chat.messages.count", history.Messages.Count);
        try
        {
            var historyText = string.Join("|", history.Messages.Select(m => $"{m.AuthorRole}:{m.Content}"));
            var cacheKey = _cache.GenerateCacheKey(historyText);
            var cachedResponse = await _cache.GetCachedResponseAsync(cacheKey);
            if (cachedResponse != null)
            {
                activity?.SetTag("chat.cache.hit", true);
                _metrics.IncrementCacheHit(ServiceType);
                _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
                _metrics.RecordResponseLength(ServiceType, cachedResponse.Length);
                return cachedResponse;
            }
            activity?.SetTag("chat.cache.hit", false);
            _metrics.IncrementCacheMiss(ServiceType);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); cts.CancelAfter(_requestTimeout);
            var result = _session.ChatAsync(history, new InferenceParams() { AntiPrompts = ["User:"] });
            var sb = new StringBuilder(capacity: 1024);
            await foreach (var r in result.WithCancellation(cts.Token).ConfigureAwait(false)) sb.Append(r);
            var response = sb.ToString();
            await _cache.SetCachedResponseAsync(cacheKey, response, TimeSpan.FromMinutes(15));
            _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
            _metrics.RecordResponseLength(ServiceType, response.Length);
            activity?.SetTag("chat.output.length", response.Length);
            _logger.LogDebug($"Generated response length={response.Length} in {stopwatch.ElapsedMilliseconds}ms");
            return response;
        }
        catch (OperationCanceledException ex)
        { activity?.SetTag("otel.status_code", "ERROR"); activity?.SetTag("exception.type", nameof(OperationCanceledException)); activity?.SetTag("exception.message", ex.Message); _metrics.IncrementErrorCount(ServiceType, "Timeout"); _logger.LogWarning($"SendAsync timeout after {_requestTimeout}"); throw new TimeoutException($"Request timed out after {_requestTimeout}"); }
        catch (Exception ex)
        { activity?.SetTag("otel.status_code", "ERROR"); activity?.SetTag("exception.type", ex.GetType().Name); activity?.SetTag("exception.message", ex.Message); _metrics.IncrementErrorCount(ServiceType, ex.GetType().Name); _logger.LogError($"Error in SendAsync: {ex.Message}"); throw; }
    }

    public void Dispose() { if (!_disposed) { _context?.Dispose(); _disposed = true; } }
}

public class HistoryTransform : DefaultHistoryTransform
{ public override string HistoryToText(ChatHistory history) => base.HistoryToText(history) + "\n Assistant:"; }
