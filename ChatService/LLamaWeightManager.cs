using Contracts;
using LLama;
using LLama.Common;
using System.Collections.Concurrent;

namespace ChatService;

/// <summary>
/// Manages lifecycle (loading, caching, fallback, disposal) of <see cref="LLamaWeights"/> keyed by resolved absolute model path.
/// Adds detailed logging for resolution, cache hits/misses, load attempts, fallbacks, and disposal.
/// </summary>
public class LLamaWeightManager : ILLamaWeightManager
{
    private readonly ConcurrentDictionary<string, WeightEntry> _weights = new();
    private readonly ILoggerManager _logger;
    private bool _disposed = false;

    public LLamaWeightManager(ILoggerManager logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LLamaWeights GetOrCreateWeights(string modelPath, ModelParams parameters)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path cannot be null or empty", nameof(modelPath));

        _logger.LogDebug($"GetOrCreateWeights invoked. RequestedPath='{modelPath}'");

        // Resolve absolute path
        string resolvedPath = modelPath;
        try
        {
            if (!Path.IsPathRooted(modelPath))
            {
                var baseDir = AppContext.BaseDirectory;
                var attempt1 = Path.GetFullPath(Path.Combine(baseDir, modelPath));
                var attempt2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", modelPath));

                if (File.Exists(attempt1))
                {
                    resolvedPath = attempt1;
                    _logger.LogDebug($"Resolved model path (primary) => '{resolvedPath}'");
                }
                else if (File.Exists(attempt2))
                {
                    resolvedPath = attempt2;
                    _logger.LogDebug($"Resolved model path (dev) => '{resolvedPath}'");
                }
                else
                {
                    _logger.LogWarning($"Unable to resolve relative model path to an existing file. Using original='{modelPath}'");
                }
            }
            else
            {
                _logger.LogDebug($"Model path already absolute: '{resolvedPath}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during path resolution for '{modelPath}': {ex.GetType().Name} - {ex.Message}");
            throw;
        }

        if (!File.Exists(resolvedPath))
        {
            _logger.LogError($"Model file not found. Original='{modelPath}' Resolved='{resolvedPath}' BaseDir='{AppContext.BaseDirectory}'");
            throw new FileNotFoundException($"LLama model file not found at '{resolvedPath}'", resolvedPath);
        }

        parameters.ModelPath = resolvedPath;

        var cacheHit = _weights.ContainsKey(resolvedPath);
        if (cacheHit)
            _logger.LogDebug($"Cache hit (weights already loaded) for '{resolvedPath}'");
        else
            _logger.LogDebug($"Cache miss. Loading weights for '{resolvedPath}'");

        // Atomic load or reuse
        try
        {
            return _weights.GetOrAdd(resolvedPath, path =>
            {
                _logger.LogInfo($"Loading LLama weights from: {path}");
                try
                {
                    _logger.LogDebug($"File size: {new FileInfo(path).Length / (1024 * 1024)} MB");
                }
                catch (Exception sizeEx)
                {
                    _logger.LogWarning($"Could not read file size for '{path}': {sizeEx.Message}");
                }
                _logger.LogDebug($"Load parameters: {SummarizeParams(parameters)}");

                try
                {
                    var weights = LLamaWeights.LoadFromFile(parameters);
                    _logger.LogInfo($"Successfully loaded LLama weights from: {path}");
                    return new WeightEntry(weights, DateTimeOffset.UtcNow, parameters.ContextSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Primary load failed for '{path}': {ex.GetType().Name} - {ex.Message}");

                    // Attempt fallback only if GPU was requested
                    if (parameters.GpuLayerCount > 0)
                    {
                        try
                        {
                            var fallback = CloneForCpuFallback(parameters);
                            _logger.LogInfo($"Attempting CPU-only fallback load. FallbackParams: {SummarizeParams(fallback)}");
                            var fallbackWeights = LLamaWeights.LoadFromFile(fallback);
                            _logger.LogInfo("Fallback (CPU-only) load succeeded.");
                            return new WeightEntry(fallbackWeights, DateTimeOffset.UtcNow, fallback.ContextSize);
                        }
                        catch (Exception inner)
                        {
                            _logger.LogError($"Fallback load also failed: {inner.GetType().Name} - {inner.Message}");
                        }
                    }

                    // Rethrow original failure if fallback not performed or failed
                    throw;
                }
            }).Weights;
        }
        catch
        {
            // Exception already logged above; preserving stack trace.
            throw;
        }
    }

    public void RemoveWeights(string modelPath)
    {
        ThrowIfDisposed();

        _logger.LogDebug($"RemoveWeights invoked for '{modelPath}'");
        try
        {
            string resolvedPath = modelPath;
            if (!Path.IsPathRooted(modelPath))
            {
                var baseDir = AppContext.BaseDirectory;
                var attempt1 = Path.GetFullPath(Path.Combine(baseDir, modelPath));
                var attempt2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", modelPath));
                if (File.Exists(attempt1)) resolvedPath = attempt1;
                else if (File.Exists(attempt2)) resolvedPath = attempt2;
            }

            if (_weights.TryRemove(resolvedPath, out var entry))
            {
                try
                {
                    entry.Weights.Dispose();
                    _logger.LogInfo($"Removed weights for model: {resolvedPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error disposing weights for '{resolvedPath}': {ex.GetType().Name} - {ex.Message}");
                }
            }
            else
            {
                _logger.LogDebug($"No cached weights found for '{resolvedPath}' (nothing to remove).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error in RemoveWeights('{modelPath}'): {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    public int GetLoadedWeightsCount()
    {
        ThrowIfDisposed();
        var count = _weights.Count;
        _logger.LogDebug($"GetLoadedWeightsCount => {count}");
        return count;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _logger.LogDebug("Disposing LLamaWeightManager and all loaded weights.");
                foreach (var kvp in _weights)
                {
                    try
                    {
                        kvp.Value.Weights.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error disposing weights for '{kvp.Key}': {ex.GetType().Name} - {ex.Message}");
                    }
                }
                _weights.Clear();
                _logger.LogInfo("LLamaWeightManager disposed");
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LLamaWeightManager));
    }

    private static ModelParams CloneForCpuFallback(ModelParams src)
    {
        var fallbackContext = Math.Min(src.ContextSize ?? 2048u, 512u);
        return new ModelParams(src.ModelPath)
        {
            ContextSize = fallbackContext,
            GpuLayerCount = 0,
            MainGpu = 0,
            SplitMode = null,
            FlashAttention = false,
            Embeddings = src.Embeddings
        };
    }

    private static string SummarizeParams(ModelParams p) =>
        $"ContextSize={p.ContextSize} MainGpu={p.MainGpu} GpuLayerCount={p.GpuLayerCount} SplitMode={p.SplitMode} FlashAttention={p.FlashAttention} Embeddings={p.Embeddings}";

    private sealed record WeightEntry(LLamaWeights Weights, DateTimeOffset LoadedAt, uint? ContextSize);
}