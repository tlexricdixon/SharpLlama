using Microsoft.Extensions.Caching.Memory;

namespace SharpLlama.Contracts;

public interface IChatResponseCache
{
    Task<string?> GetCachedResponseAsync(string cacheKey);
    Task SetCachedResponseAsync(string cacheKey, string response, TimeSpan? expiration = null);
    void RemoveCachedResponse(string cacheKey);
    string GenerateCacheKey(string input, string? context = null);
}