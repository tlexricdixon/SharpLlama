using Contracts;
using Infrastructure;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChatService;

public sealed class StatefulChatService : IDisposable, IStatefulChatService
{
    private static readonly ActivitySource Activity = new("SharpLlama.Chat");
    private readonly ChatSession _session;
    private readonly LLamaContext _context;
    private readonly ILoggerManager _logger;
    private readonly IChatResponseCache _cache;
    private readonly IChatMetrics _metrics;
    private readonly InferenceParams _defaultInferenceParams;
    private readonly TimeSpan _requestTimeout;
    private bool _continue = false;
    private bool _disposed = false;
    private const string ServiceType = "StatefulChatService";
    private const string SystemPrompt = "Transcript of a dialog, where the User interacts with an Assistant. Assistant is helpful, kind, honest, good at writing, and never fails to answer the User's requests immediately and with precision.";

    public StatefulChatService(
        IOptions<ModelOptions> modelOptions,
        IOptions<ChatServiceOptions> chatOptions,
        ILoggerManager logger,
        ILLamaWeightManager weightManager,
        IChatResponseCache? cache = null,
        IChatMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? new NullChatResponseCache();
        _metrics = metrics ?? new NullChatMetrics();

        var m = modelOptions.Value;
        var c = chatOptions.Value;

        var modelPath = m.ModelPath ?? throw new InvalidOperationException("ModelPath option is required");
        if (!Path.IsPathRooted(modelPath))
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, modelPath));
            if (File.Exists(candidate)) modelPath = candidate;
            else
            {
                var devCandidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", modelPath));
                if (File.Exists(devCandidate)) modelPath = devCandidate;
            }
        }

        _requestTimeout = TimeSpan.FromSeconds(c.RequestTimeoutSeconds);
        var @params = new ModelParams(modelPath) { ContextSize = (uint)m.ContextSize };

        try
        {
            var weights = weightManager.GetOrCreateWeights(modelPath, @params);
            _context = new LLamaContext(weights, @params);
            _session = new ChatSession(new InteractiveExecutor(_context));
            _session.History.AddMessage(AuthorRole.System, SystemPrompt);

            _defaultInferenceParams = new InferenceParams
            {
                AntiPrompts = c.AntiPrompts,
                MaxTokens = c.MaxTokens,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    RepeatPenalty = (float)c.RepeatPenalty,
                    Temperature = (float)c.Temperature,
                    TopP = (float)c.TopP
                }
            };

            _logger.LogInfo($"evt=ServiceInit service={ServiceType} modelFile={Path.GetFileName(modelPath)} contextSize={m.ContextSize} timeoutSec={_requestTimeout.TotalSeconds} antiPrompts=\"{string.Join('|', c.AntiPrompts)}\" maxTokens={c.MaxTokens} temp={c.Temperature} topP={c.TopP} repeatPenalty={c.RepeatPenalty}");
        }
        catch
        {
            _context?.Dispose();
            throw;
        }
    }

    public async Task<string> Send(Entities.SendMessageInput input)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StatefulChatService));
        if (input?.Text == null) throw new ArgumentException("Input text cannot be null", nameof(input));
        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementRequestCount(ServiceType);
        using var activity = Activity.StartActivity("chat.stateful.send", ActivityKind.Server);
        activity?.SetTag("chat.service", ServiceType);
        activity?.SetTag("chat.input.length", input.Text.Length);
        try
        {
            var cacheKey = _cache.GenerateCacheKey(input.Text, _session.History.Messages.LastOrDefault()?.Content);
            var cachedResponse = await _cache.GetCachedResponseAsync(cacheKey);
            if (cachedResponse != null)
            {
                activity?.SetTag("chat.cache.hit", true);
                _metrics.IncrementCacheHit(ServiceType);
                _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
                _metrics.RecordResponseLength(ServiceType, cachedResponse.Length);
                _logger.LogInfo($"evt=CacheHit service={ServiceType} keyHash={cacheKey.GetHashCode()} responseLength={cachedResponse.Length}");
                return cachedResponse;
            }
            activity?.SetTag("chat.cache.hit", false);
            _metrics.IncrementCacheMiss(ServiceType);
            _logger.LogInfo($"evt=CacheMiss service={ServiceType} keyHash={cacheKey.GetHashCode()}");
            using var cts = new CancellationTokenSource(_requestTimeout);
            if (!_continue)
            {
                _logger.LogInfo($"evt=EmitSystemPrompt service={ServiceType} promptChars={SystemPrompt.Length}");
                _continue = true;
            }
            _logger.LogInfo($"evt=UserMessage service={ServiceType} chars={input.Text.Length}");
            var outputs = _session.ChatAsync(new ChatHistory.Message(AuthorRole.User, input.Text), _defaultInferenceParams);
            var resultBuilder = new System.Text.StringBuilder(1024);
            await foreach (var output in outputs.WithCancellation(cts.Token).ConfigureAwait(false))
            {
                _logger.LogDebug(output);
                resultBuilder.Append(output);
            }
            var result = resultBuilder.ToString();
            await _cache.SetCachedResponseAsync(cacheKey, result, TimeSpan.FromMinutes(15));
            _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
            _metrics.RecordResponseLength(ServiceType, result.Length);
            activity?.SetTag("chat.output.length", result.Length);
            _logger.LogInfo($"evt=GenerationComplete service={ServiceType} responseLength={result.Length} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return result;
        }
        catch (OperationCanceledException ex)
        {
            activity?.SetTag("otel.status_code", "ERROR");
            activity?.SetTag("exception.type", nameof(OperationCanceledException));
            activity?.SetTag("exception.message", ex.Message);
            _metrics.IncrementErrorCount(ServiceType, "Timeout");
            _logger.LogWarning($"evt=Timeout service={ServiceType} timeoutSec={_requestTimeout.TotalSeconds}");
            throw new TimeoutException($"Request timed out after {_requestTimeout}");
        }
        catch (Exception ex)
        {
            activity?.SetTag("otel.status_code", "ERROR");
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            _metrics.IncrementErrorCount(ServiceType, ex.GetType().Name);
            _logger.LogError($"evt=UnhandledError service={ServiceType} errorType={ex.GetType().Name} message=\"{ex.Message}\"");
            throw;
        }
    }

    public async IAsyncEnumerable<string> SendStream(
        Entities.SendMessageInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StatefulChatService));
        if (input?.Text == null) throw new ArgumentException("Input text cannot be null", nameof(input));

        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementRequestCount($"{ServiceType}Stream");
        using var activity = Activity.StartActivity("chat.stateful.stream", ActivityKind.Server);
        activity?.SetTag("chat.service", ServiceType);
        activity?.SetTag("chat.input.length", input.Text.Length);

        using var timeoutCts = new CancellationTokenSource(_requestTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        if (!_continue)
        {
            _logger.LogInfo($"evt=EmitSystemPrompt service={ServiceType} promptChars={SystemPrompt.Length}");
            _continue = true;
        }
        _logger.LogInfo($"evt=UserMessage service={ServiceType} stream=true chars={input.Text.Length}");

        var outputs = _session.ChatAsync(new ChatHistory.Message(AuthorRole.User, input.Text), _defaultInferenceParams);
        var responseBuilder = new System.Text.StringBuilder();
        Exception? streamException = null;

        try
        {
            await foreach (var output in outputs.WithCancellation(linkedCts.Token).ConfigureAwait(false))
            {
                if (linkedCts.IsCancellationRequested) break;
                _logger.LogDebug(output);
                responseBuilder.Append(output);
                yield return output;
            }
        }
        finally
        {
            if (streamException == null && !linkedCts.IsCancellationRequested)
            {
                _metrics.RecordResponseTime($"{ServiceType}Stream", stopwatch.Elapsed);
                _metrics.RecordResponseLength($"{ServiceType}Stream", responseBuilder.Length);
                activity?.SetTag("chat.output.length", responseBuilder.Length);
                _logger.LogInfo($"evt=StreamComplete service={ServiceType} responseLength={responseBuilder.Length} elapsedMs={stopwatch.ElapsedMilliseconds}");
            }
            else if (streamException != null)
            {
                var errorType = streamException is TimeoutException ? "Timeout" : streamException.GetType().Name;
                _metrics.IncrementErrorCount($"{ServiceType}Stream", errorType);
                activity?.SetTag("otel.status_code", "ERROR");
                activity?.SetTag("exception.type", streamException.GetType().Name);
                activity?.SetTag("exception.message", streamException.Message);
                _logger.LogError($"evt=StreamError service={ServiceType} errorType={streamException.GetType().Name} message=\"{streamException.Message}\"");
                throw streamException;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _context?.Dispose();
            _disposed = true;
        }
    }
}