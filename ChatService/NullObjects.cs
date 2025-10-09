using SharpLlama.Contracts;

namespace SharpLlama.ChatService;

internal class NullChatResponseCache : IChatResponseCache
{
    public Task<string?> GetCachedResponseAsync(string cacheKey) => Task.FromResult<string?>(null);
    public Task SetCachedResponseAsync(string cacheKey, string response, TimeSpan? expiration = null) => Task.CompletedTask;
    public void RemoveCachedResponse(string cacheKey) { }
    public string GenerateCacheKey(string input, string? context = null) => string.Empty;
}

internal class NullChatMetrics : IChatMetrics
{
    public void IncrementRequestCount(string serviceType) { }
    public void RecordResponseTime(string serviceType, TimeSpan duration) { }
    public void RecordResponseLength(string serviceType, int length) { }
    public void IncrementErrorCount(string serviceType, string errorType) { }
    public void IncrementCacheHit(string serviceType) { }
    public void IncrementCacheMiss(string serviceType) { }
    public ChatMetricsSnapshot GetSnapshot() => new(0,0,0,0,0,0,0,0,0,0,0,0,new(),new());
}
