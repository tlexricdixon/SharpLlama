using ChatService.Plugins;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using SharpLlama.Contracts;
using System.Diagnostics;
using System.Text;
using static LLama.LLamaTransforms;

namespace SharpLlama.ChatService;

/// <summary>
/// Stateless chat service that:
/// 1. Validates and pre-processes the latest user message through a pluggable pipeline.
/// 2. Leverages a llama.cpp backed <see cref="LLamaContext"/> and <see cref="ChatSession"/> to produce a response.
/// 3. Applies post-processing plugins to refine / normalize output.
/// 4. Caches full-history keyed responses to reduce recomputation.
/// 5. Emits metrics (requests, latency, errors, cache efficiency).
/// 
/// Although named "stateless", this service treats each invocation independently;
/// any conversational continuity must be supplied by the caller through the <see cref="ChatHistory"/> object.
/// </summary>
public class EnhancedStatelessChatService : IStatelessChatService
{
    private readonly LLamaContext _context;
    private readonly ChatSession _session;
    private readonly ILoggerManager _logger;
    private readonly IChatResponseCache _cache;
    private readonly IChatMetrics _metrics;
    private readonly Kernel _kernel;
    private readonly List<ISemanticKernelPlugin> _plugins;
    private readonly int _contextSize;
    private readonly TimeSpan _requestTimeout;
    private bool _disposed = false;
    private const string ServiceType = "EnhancedStatelessChatService";

    public EnhancedStatelessChatService(
        IConfiguration configuration,
        ILoggerManager logger,
        ILLamaWeightManager weightManager,
        IChatResponseCache? cache = null,
        IChatMetrics? metrics = null,
        IEnumerable<ISemanticKernelPlugin>? plugins = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? new NullChatResponseCache();
        _metrics = metrics ?? new NullChatMetrics();

        var modelPath = configuration["ModelPath"] ?? throw new InvalidOperationException("ModelPath is required in configuration");
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
        _contextSize = int.TryParse(configuration["ContextSize"], out var contextSize) ? contextSize : 2048;
        var timeoutSeconds = int.TryParse(configuration["ChatService:RequestTimeoutSeconds"], out var timeout) ? timeout : 120;
        _requestTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        _logger.LogInfo("Initializing {service} (contextSize={ctx} timeout={timeout}s)", ServiceType, _contextSize, _requestTimeout.TotalSeconds);

        var kernelBuilder = Kernel.CreateBuilder();
        _kernel = kernelBuilder.Build();

        _plugins = plugins?.ToList() ?? new List<ISemanticKernelPlugin> { new InputValidationPlugin(), new ContextEnhancementPlugin(_cache), new ResponseFormattingPlugin() };
        _logger.LogDebug($"Using plugins: {string.Join(", ", _plugins.Select(p => p.Name))}");

        foreach (var plugin in _plugins)
        {
            try { _kernel.Plugins.AddFromObject(plugin, plugin.Name); _logger.LogDebug($"Registered plugin '{plugin.Name}'"); }
            catch (Exception ex) { _logger.LogError($"Failed to register plugin '{plugin.Name}': {ex.Message}"); throw; }
        }

        ModelParams @params;
        try { @params = BuildModelParams(configuration, modelPath, (uint)_contextSize); }
        catch (Exception ex) { _logger.LogError($"Error building model parameters: {ex.Message}"); throw; }
        try
        {
            var weights = weightManager.GetOrCreateWeights(modelPath, @params);
            _context = new LLamaContext(weights, @params);
            var executor = new InteractiveExecutor(_context);

            _session = new ChatSession(executor)
                .WithOutputTransform(new KeywordTextOutputStreamTransform(["User:", "Assistant:"], redundancyLength: 8))
                .WithHistoryTransform(new HistoryTransform());
            _logger.LogInfo("{service} initialized with {pluginCount} plugins context={ctx}", ServiceType, _plugins.Count, _contextSize);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to initialize llama context/session: {ex.Message}");
            _context?.Dispose();
            throw;
        }
    }

    public async Task<string> SendAsync(ChatHistory history, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EnhancedStatelessChatService));
        if (history == null) throw new ArgumentNullException(nameof(history));
        var stopwatch = Stopwatch.StartNew();
        _metrics.IncrementRequestCount(ServiceType);
        var context = new Dictionary<string, object>();
        string lastMessage = string.Empty;
        string cacheKey = string.Empty;
        _logger.LogDebug($"SendAsync invoked. Total messages: {history.Messages.Count}");
        try
        {
            lastMessage = GetLastUserMessage(history);
            if (string.IsNullOrWhiteSpace(lastMessage)) _logger.LogWarning("Last user message is empty or missing.");
            var processedInput = await ApplyPreProcessingPluginsAsync(lastMessage, context);
            var historyText = string.Join("|", history.Messages.Select(m => $"{m.AuthorRole}:{m.Content}"));
            cacheKey = _cache.GenerateCacheKey(historyText);
            _logger.LogDebug($"Cache key: {cacheKey}");
            string? cachedResponse = null;
            try { cachedResponse = await _cache.GetCachedResponseAsync(cacheKey); } catch (Exception ex) { _logger.LogError($"Cache retrieval error (Key={cacheKey}): {ex.Message}"); }
            if (cachedResponse != null)
            {
                _metrics.IncrementCacheHit(ServiceType);
                _logger.LogDebug($"Cache hit (Key={cacheKey}). Running post-processing.");
                var formattedCache = await ApplyPostProcessingPluginsAsync(cachedResponse, lastMessage, context);
                _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
                _logger.LogDebug($"Cache hit length={formattedCache.Length} ms={stopwatch.ElapsedMilliseconds}");
                return formattedCache;
            }
            _metrics.IncrementCacheMiss(ServiceType);
            if (processedInput != lastMessage) history = UpdateLastMessage(history, processedInput);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_requestTimeout);
            var result = _session.ChatAsync(history, new InferenceParams { AntiPrompts = ["User:"] });
            var sb = new StringBuilder(1024);
            try
            {
                await foreach (var r in result.WithCancellation(cts.Token).ConfigureAwait(false)) sb.Append(r);
            }
            catch (OperationCanceledException) { _logger.LogWarning("Generation canceled/timeout"); throw; }
            var response = sb.ToString();
            var finalResponse = await ApplyPostProcessingPluginsAsync(response, lastMessage, context);
            try { await _cache.SetCachedResponseAsync(cacheKey, finalResponse, TimeSpan.FromMinutes(15)); } catch (Exception ex) { _logger.LogError($"Failed to cache response (Key={cacheKey}): {ex.Message}"); }
            _metrics.RecordResponseTime(ServiceType, stopwatch.Elapsed);
            _metrics.RecordResponseLength(ServiceType, finalResponse.Length);
            _logger.LogDebug($"Generated response length={finalResponse.Length} ms={stopwatch.ElapsedMilliseconds}");
            return finalResponse;
        }
        catch (ArgumentException argEx)
        {
            _metrics.IncrementErrorCount(ServiceType, "Validation");
            _logger.LogWarning($"Validation error: {argEx.Message}");
            throw;
        }
        catch (OperationCanceledException)
        {
            _metrics.IncrementErrorCount(ServiceType, "Timeout");
            _logger.LogWarning($"SendAsync timeout after {_requestTimeout}");
            throw new TimeoutException($"Request timed out after {_requestTimeout}");
        }
        catch (Exception ex)
        {
            _metrics.IncrementErrorCount(ServiceType, ex.GetType().Name);
            _logger.LogError($"Unhandled error in SendAsync: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
        finally { stopwatch.Stop(); }
    }

    private async Task<string> ApplyPreProcessingPluginsAsync(string input, Dictionary<string, object> context)
    {
        var processedInput = input;
        foreach (var plugin in _plugins.OfType<IValidationPlugin>())
        {
            try
            {
                _logger.LogDebug($"Validating with '{plugin.GetType().Name}'");
                var validation = await plugin.ValidateAsync(processedInput);
                if (!validation.IsValid) throw new ArgumentException($"Input validation failed: {validation.ErrorMessage}");
                if (!string.IsNullOrEmpty(validation.SanitizedInput) && validation.SanitizedInput != processedInput)
                    processedInput = validation.SanitizedInput;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            { _logger.LogError($"Validation plugin '{plugin.GetType().Name}' threw: {ex.Message}"); throw; }
        }
        foreach (var plugin in _plugins.OfType<IPreProcessingPlugin>())
        {
            try
            {
                if (await plugin.CanHandleAsync(processedInput)) processedInput = await plugin.PreProcessAsync(processedInput, context);
            }
            catch (Exception ex) { _logger.LogError($"Pre-processing plugin '{plugin.GetType().Name}' failed: {ex.Message}"); throw; }
        }
        return processedInput;
    }

    private async Task<string> ApplyPostProcessingPluginsAsync(string output, string originalInput, Dictionary<string, object> context)
    {
        var processedOutput = output;
        foreach (var plugin in _plugins.OfType<IPostProcessingPlugin>())
        {
            try
            {
                if (await plugin.CanHandleAsync(processedOutput)) processedOutput = await plugin.PostProcessAsync(processedOutput, originalInput, context);
            }
            catch (Exception ex) { _logger.LogError($"Post-processing plugin '{plugin.GetType().Name}' failed: {ex.Message}"); throw; }
        }
        return processedOutput;
    }

    private static string GetLastUserMessage(ChatHistory history)
    {
        var lastMessage = history.Messages.LastOrDefault(m => m.AuthorRole == AuthorRole.User);
        return lastMessage?.Content ?? string.Empty;
    }

    private static ChatHistory UpdateLastMessage(ChatHistory history, string newContent)
    {
        var newHistory = new ChatHistory();
        var messages = history.Messages.ToList();

        for (int i = 0; i < messages.Count; i++)
        {
            if (i == messages.Count - 1 && messages[i].AuthorRole == AuthorRole.User)
            {
                newHistory.AddMessage(AuthorRole.User, newContent);
            }
            else
            {
                newHistory.AddMessage(messages[i].AuthorRole, messages[i].Content);
            }
        }

        return newHistory;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                _logger.LogDebug($"Disposing {ServiceType} resources.");
                _context?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during disposal: {ex.Message}");
                throw;
            }
            finally
            {
                _disposed = true;
                _logger.LogDebug($"{ServiceType} disposed.");
            }
        }
    }

    private static ModelParams BuildModelParams(IConfiguration config, string modelPath, uint contextSize)
    {
        var p = new ModelParams(modelPath)
        {
            ContextSize = contextSize
        };

        if (!int.TryParse(config["LLama:MainGpu"], out var mainGpu))
            mainGpu = 1;
        p.MainGpu = mainGpu;

        if (int.TryParse(config["LLama:GpuLayerCount"], out var gpuLayers) && gpuLayers >= 0)
            p.GpuLayerCount = gpuLayers;

        var splitModeStr = config["LLama:SplitMode"];
        if (!string.IsNullOrEmpty(splitModeStr) &&
            Enum.TryParse<GPUSplitMode>(splitModeStr, true, out var splitMode))
            p.SplitMode = splitMode;

        if (bool.TryParse(config["LLama:FlashAttention"], out var flash))
            p.FlashAttention = flash;

        return p;
    }
}

