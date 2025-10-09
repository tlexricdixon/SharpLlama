using SharpLlama.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace SharpLlama.ChatService;

public class ChatMetrics : IChatMetrics
{
    // ---- OpenTelemetry Instruments (renamed to semantic-style) ----
    private static readonly Meter Meter = new("SharpLlama.Chat");

    private static readonly Counter<long> RequestsCounter = Meter.CreateCounter<long>("llm.requests", description: "Total LLM-related requests (chat and RAG).");
    private static readonly Counter<long> ErrorsCounter = Meter.CreateCounter<long>("llm.errors", description: "Total LLM errors.");
    private static readonly Counter<long> CacheHitCounter = Meter.CreateCounter<long>("rag.cache.hits", description: "RAG cache hits.");
    private static readonly Counter<long> CacheMissCounter = Meter.CreateCounter<long>("rag.cache.misses", description: "RAG cache misses.");
    private static readonly Histogram<double> ResponseDurationMs = Meter.CreateHistogram<double>("llm.response.duration", unit: "ms", description: "LLM response latency (ms).");
    private static readonly Histogram<int> ResponseLengthChars = Meter.CreateHistogram<int>("llm.response.length", unit: "chars", description: "Final response length (characters).");

    private static long _globalCacheHits;
    private static long _globalCacheMisses;

    static ChatMetrics()
    {
        Meter.CreateObservableGauge("rag.cache.hit_ratio",
            () =>
            {
                var hits = Interlocked.Read(ref _globalCacheHits);
                var misses = Interlocked.Read(ref _globalCacheMisses);
                var total = hits + misses;
                double ratio = total == 0 ? 0 : (double)hits / total;
                return new[] { new Measurement<double>(ratio) };
            },
            description: "Cache hit ratio (hits / (hits+misses)).");
    }

    // ---- EXISTING FIELDS ----
    private readonly ConcurrentDictionary<string, long> _requestCounts = new();
    private readonly ConcurrentDictionary<string, long> _errorCounts = new();
    private readonly ConcurrentDictionary<string, List<double>> _responseTimes = new();
    private readonly ConcurrentDictionary<string, List<int>> _responseLengths = new();
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private readonly object _lock = new();
    private readonly ILogger<ChatMetrics> _logger;

    public ChatMetrics(ILogger<ChatMetrics> logger)
    {
        _logger = logger;
        _logger.LogInformation("ChatMetrics initialized");
    }

    public void IncrementRequestCount(string serviceType)
    {
        try
        {
            _requestCounts.AddOrUpdate(serviceType, 1, static (_, v) => v + 1);
            RequestsCounter.Add(1, new KeyValuePair<string, object?>("chat.service", serviceType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment request count for {ServiceType}", serviceType);
        }
    }

    public void RecordResponseTime(string serviceType, TimeSpan duration)
    {
        try
        {
            var ms = duration.TotalMilliseconds;
            _responseTimes.AddOrUpdate(serviceType,
                _ => new List<double> { ms },
                (_, list) =>
                {
                    lock (_lock)
                    {
                        list.Add(ms);
                        Trim(list);
                        return list;
                    }
                });
            ResponseDurationMs.Record(ms, new KeyValuePair<string, object?>("chat.service", serviceType));
            _logger.LogDebug("Recorded response time {Duration} ms for {ServiceType}", ms, serviceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record response time for {ServiceType}", serviceType);
        }
    }

    public void RecordResponseLength(string serviceType, int length)
    {
        try
        {
            _responseLengths.AddOrUpdate(serviceType,
                _ => new List<int> { length },
                (_, list) =>
                {
                    lock (_lock)
                    {
                        list.Add(length);
                        Trim(list);
                        return list;
                    }
                });
            ResponseLengthChars.Record(length, new KeyValuePair<string, object?>("chat.service", serviceType));
            _logger.LogDebug("Recorded response length {Length} chars for {ServiceType}", length, serviceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record response length for {ServiceType}", serviceType);
        }
    }

    public void IncrementErrorCount(string serviceType, string errorType)
    {
        var key = $"{serviceType}:{errorType}";
        try
        {
            _errorCounts.AddOrUpdate(key, 1, static (_, v) => v + 1);
            ErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("chat.service", serviceType),
                new KeyValuePair<string, object?>("error.type", errorType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment error count for {ServiceType} {ErrorType}", serviceType, errorType);
        }
    }

    public void IncrementCacheHit(string serviceType)
    {
        try
        {
            Interlocked.Increment(ref _cacheHits);
            Interlocked.Increment(ref _globalCacheHits);
            CacheHitCounter.Add(1, new KeyValuePair<string, object?>("chat.service", serviceType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed cache hit inc {ServiceType}", serviceType);
        }
    }

    public void IncrementCacheMiss(string serviceType)
    {
        try
        {
            Interlocked.Increment(ref _cacheMisses);
            Interlocked.Increment(ref _globalCacheMisses);
            CacheMissCounter.Add(1, new KeyValuePair<string, object?>("chat.service", serviceType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed cache miss inc {ServiceType}", serviceType);
        }
    }

    public ChatMetricsSnapshot GetSnapshot()
    {
        try
        {
            List<double> times;
            List<int> lengths;
            lock (_lock)
            {
                times = _responseTimes.Values.SelectMany(v => v.ToArray()).ToList();
                lengths = _responseLengths.Values.SelectMany(v => v.ToArray()).ToList();
            }
            var totalRequests = _requestCounts.Values.Sum();
            var totalErrors = _errorCounts.Values.Sum();
            double avgTime = times.Count > 0 ? times.Average() : 0;
            double avgLen = lengths.Count > 0 ? lengths.Average() : 0;
            double p50t = Percentile(times, 50);
            double p95t = Percentile(times, 95);
            double p99t = Percentile(times, 99);
            double p50l = Percentile(lengths.Select(x => (double)x).ToList(), 50);
            double p95l = Percentile(lengths.Select(x => (double)x).ToList(), 95);
            double p99l = Percentile(lengths.Select(x => (double)x).ToList(), 99);
            return new ChatMetricsSnapshot(totalRequests, totalErrors, _cacheHits, _cacheMisses, avgTime, avgLen, p50t, p95t, p99t, p50l, p95l, p99l,
                new Dictionary<string, long>(_requestCounts), new Dictionary<string, long>(_errorCounts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate metrics snapshot");
            return new ChatMetricsSnapshot(
                0, 0, _cacheHits, _cacheMisses, 0, 0, 0, 0, 0, 0, 0, 0, new(), new());
        }
    }

    private static void Trim<T>(List<T> list)
    {
        if (list.Count > 1000) list.RemoveRange(0, list.Count - 1000);
    }

    private static double Percentile(List<double> data, double percentile)
    {
        if (data.Count == 0) return 0;
        data.Sort();
        var rank = (percentile / 100.0) * (data.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high) return data[low];
        var weight = rank - low;
        return data[low] + (data[high] - data[low]) * weight;
    }
}

public static class ChatOtelMetrics
{
    private static readonly Meter Meter = new("SharpLlama.Chat");
    private static readonly Histogram<int> RagContextSize = Meter.CreateHistogram<int>("rag.context.size", unit: "chars", description: "RAG context size (characters).");
    private static readonly Histogram<int> OutputTokens = Meter.CreateHistogram<int>("llm.usage.output_tokens", unit: "tokens", description: "Output tokens.");
    private static readonly Histogram<int> PromptTokens = Meter.CreateHistogram<int>("llm.usage.input_tokens", unit: "tokens", description: "Input prompt tokens.");
    private static readonly Histogram<int> EstimatedPromptTokens = Meter.CreateHistogram<int>("llm.usage.prompt_tokens.estimated", unit: "tokens", description: "Estimated input prompt tokens (char/4 heuristic).");

    private static int _lastPromptTokens;
    private static int _lastOutputTokens;
    private static int _contextWindowSize;

    static ChatOtelMetrics()
    {
        Meter.CreateObservableGauge("llm.usage.total_tokens",
            () => new[] { new Measurement<int>(_lastPromptTokens + _lastOutputTokens) },
            unit: "tokens",
            description: "Total tokens (input + output) for last recorded generation.");

        Meter.CreateObservableGauge("llm.context.usage.percent",
            () =>
            {
                if (_contextWindowSize <= 0) return Array.Empty<Measurement<double>>();
                double pct = (_lastPromptTokens + _lastOutputTokens) * 100.0 / _contextWindowSize;
                return new[] { new Measurement<double>(Math.Round(pct, 2)) };
            },
            unit: "%",
            description: "Percent of context window used by last generation.");
    }

    public static void RecordContextSize(int size, string serviceType)
    {
        if (size <= 0) return;
        RagContextSize.Record(size, new KeyValuePair<string, object?>("chat.service", serviceType));
    }

    public static void RecordOutputTokens(int tokens, string serviceType, int contextWindowSize)
    {
        if (tokens <= 0) return;
        _lastOutputTokens = tokens;
        _contextWindowSize = contextWindowSize;
        OutputTokens.Record(tokens, new KeyValuePair<string, object?>("chat.service", serviceType));
    }

    public static void RecordPromptTokens(int tokens, string serviceType, int contextWindowSize)
    {
        if (tokens <= 0) return;
        _lastPromptTokens = tokens;
        _contextWindowSize = contextWindowSize;
        PromptTokens.Record(tokens, new KeyValuePair<string, object?>("chat.service", serviceType));
    }

    // NEW: estimated (char/4) prompt token recorder (used before model tokenization is available)
    public static void RecordEstimatedPromptTokens(int tokens, string serviceType, int contextWindowSize)
    {
        if (tokens <= 0) return;
        _lastPromptTokens = tokens; // treat as last prompt tokens for gauges
        _contextWindowSize = contextWindowSize;
        EstimatedPromptTokens.Record(tokens,
            new KeyValuePair<string, object?>("chat.service", serviceType),
            new KeyValuePair<string, object?>("llm.tokens.estimated", true));
    }
}