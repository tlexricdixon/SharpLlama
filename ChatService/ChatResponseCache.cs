using Microsoft.Extensions.Caching.Memory;
using SharpLlama.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace SharpLlama.ChatService;

/// <summary>
/// Provides in-memory caching for chat responses, keyed by a hashed representation
/// of input (and optional contextual) data. This helps avoid recomputation or
/// re-fetching of identical responses for a period of time.
/// </summary>
/// <remarks>
/// Characteristics:
/// - Backed by <see cref="IMemoryCache"/> (process-bound; not distributed).
/// - Uses SHA-256 hashing to produce deterministic fixed-length cache keys,
///   obfuscating the original inputs.
/// - Default absolute expiration is 30 minutes unless overridden per entry.
/// - All operations are safe to call concurrently; underlying <see cref="IMemoryCache"/>
///   handles thread safety.
/// </remarks>
public class ChatResponseCache : IChatResponseCache
{
    private const int KeyPreviewLength = 20;

    /// <summary>
    /// Underlying in-memory cache instance.
    /// </summary>
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Logger for diagnostic output.
    /// </summary>
    private readonly ILoggerManager _logger;

    /// <summary>
    /// Default absolute expiration applied when no explicit value is supplied.
    /// </summary>
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Creates a new <see cref="ChatResponseCache"/> instance.
    /// </summary>
    /// <param name="cache">The <see cref="IMemoryCache"/> implementation to use.</param>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="cache"/> or <paramref name="logger"/> is null.</exception>
    public ChatResponseCache(IMemoryCache cache, ILoggerManager logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to retrieve a previously cached response.
    /// </summary>
    /// <param name="cacheKey">The hash-based cache key (see <see cref="GenerateCacheKey(string, string?)"/>).</param>
    /// <returns>
    /// A task producing the cached response string if present; otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Logs a cache hit/miss (truncating the key for readability).
    /// </remarks>
    public Task<string?> GetCachedResponseAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _logger.LogWarning("GetCachedResponseAsync called with null/empty cacheKey.");
            return Task.FromResult<string?>(null);
        }

        try
        {
            if (_cache.TryGetValue(cacheKey, out string? cachedResponse))
            {
                _logger.LogDebug($"Cache hit for key: {Preview(cacheKey)}");
                return Task.FromResult(cachedResponse);
            }

            _logger.LogDebug($"Cache miss for key: {Preview(cacheKey)}");
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving cache entry for key {Preview(cacheKey)}. Exception: {ex}");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Inserts or overwrites a cached response.
    /// </summary>
    /// <param name="cacheKey">The hash-based cache key.</param>
    /// <param name="response">The response content to cache.</param>
    /// <param name="expiration">
    /// Optional absolute expiration relative to now. If <c>null</c>, a default of 30 minutes is used.
    /// </param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// No action is taken if <paramref name="cacheKey"/> or <paramref name="response"/> is null or empty.
    /// </remarks>
    public Task SetCachedResponseAsync(string cacheKey, string response, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _logger.LogWarning("SetCachedResponseAsync called with null/empty cacheKey.");
            return Task.CompletedTask;
        }
        if (string.IsNullOrEmpty(response))
        {
            _logger.LogWarning($"SetCachedResponseAsync ignored empty response for key {Preview(cacheKey)}.");
            return Task.CompletedTask;
        }

        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, response, options);
            _logger.LogDebug($"Cached response for key: {Preview(cacheKey)} (ttl={(expiration ?? _defaultExpiration).TotalMinutes:F0}m)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error setting cache entry for key {Preview(cacheKey)}. Exception: {ex}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a cached response if it exists.
    /// </summary>
    /// <param name="cacheKey">The hash-based cache key.</param>
    /// <remarks>
    /// Silently returns if the key is null or empty.
    /// </remarks>
    public void RemoveCachedResponse(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _logger.LogWarning("RemoveCachedResponse called with null/empty cacheKey.");
            return;
        }

        try
        {
            _cache.Remove(cacheKey);
            _logger.LogDebug($"Removed cached response for key: {Preview(cacheKey)}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing cache entry for key {Preview(cacheKey)}. Exception: {ex}");
        }
    }

    /// <summary>
    /// Generates a deterministic SHA-256 hexadecimal cache key from input and optional context.
    /// </summary>
    /// <param name="input">Primary input string (e.g., user prompt or normalized request payload).</param>
    /// <param name="context">
    /// Optional contextual discriminator (e.g., model name, tenant, or system prompt hash) to reduce collisions across domains.
    /// </param>
    /// <returns>A 64-character uppercase hexadecimal string representing the SHA-256 hash.</returns>
    /// <remarks>
    /// Using a hash:
    /// - Obfuscates potentially sensitive raw input.
    /// - Produces a fixed length key, suitable for memory cache constraints.
    /// - Minimizes accidental collisions compared to simpler hashes like MD5 or SHA-1.
    /// </remarks>
    public string GenerateCacheKey(string input, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogWarning("GenerateCacheKey called with null/empty input; returning empty key.");
            return string.Empty;
        }

        try
        {
            var combined = context != null ? $"{context}:{input}" : input;
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            var key = Convert.ToHexString(hash);
            _logger.LogDebug($"Generated cache key: {Preview(key)} (context supplied: {(!string.IsNullOrEmpty(context))})");
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating cache key. Exception: {ex}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Produces a short, safe preview of a cache key for logging.
    /// </summary>
    private static string Preview(string key) =>
        string.IsNullOrEmpty(key)
            ? "<empty>"
            : (key.Length <= KeyPreviewLength ? key : key[..KeyPreviewLength]) + "...";
}