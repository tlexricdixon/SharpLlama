namespace Contracts;

public interface IChatMetrics
{
    void IncrementRequestCount(string serviceType);
    void RecordResponseTime(string serviceType, TimeSpan duration);
    void RecordResponseLength(string serviceType, int length);
    void IncrementErrorCount(string serviceType, string errorType);
    void IncrementCacheHit(string serviceType);
    void IncrementCacheMiss(string serviceType);
    ChatMetricsSnapshot GetSnapshot();
}

public record ChatMetricsSnapshot(
    long TotalRequests,
    long TotalErrors,
    long CacheHits,
    long CacheMisses,
    double AverageResponseTimeMs,
    double AverageResponseLength,
    double P50ResponseTimeMs,
    double P95ResponseTimeMs,
    double P99ResponseTimeMs,
    double P50ResponseLength,
    double P95ResponseLength,
    double P99ResponseLength,
    Dictionary<string, long> RequestsByService,
    Dictionary<string, long> ErrorsByType
);