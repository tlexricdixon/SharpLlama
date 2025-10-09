using SharpLlama.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace SharpLlama.ChatService;

/// <summary>
/// Simple in-process LRU (least recently used) cache for chat responses.
/// Bounded by entry count (capacity) to prevent unbounded memory growth.
/// </summary>
/// <remarks>
/// Thread-safety: All mutating operations guarded by a single lock (coarse grained, low contention expected).
/// LRU Maintenance: Doubly linked list (most recent at head). Dictionary maps key -> node for O(1) lookups.
/// Expiration: Optional absolute expiration per entry (default 30 minutes). Expired entries are purged lazily on access.
/// </remarks>
public sealed class LruChatResponseCache : IChatResponseCache
{
    private sealed class Entry
    {
        public required string Key { get; init; }
        public required string Value { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private readonly int _capacity;
    private readonly TimeSpan _defaultTtl;
    private readonly ILoggerManager _logger;

    // LRU bookkeeping
    private readonly Dictionary<string, LinkedListNode<Entry>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private readonly object _lock = new();

    public LruChatResponseCache(int capacity, ILoggerManager logger, TimeSpan? defaultTtl = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(30);
        _logger.LogInfo($"LruChatResponseCache initialized capacity={_capacity} ttl={_defaultTtl.TotalMinutes:F0}m");
    }

    public Task<string?> GetCachedResponseAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return Task.FromResult<string?>(null);

        lock (_lock)
        {
            if (_map.TryGetValue(cacheKey, out var node))
            {
                if (IsExpired(node.Value))
                {
                    RemoveNode(node);
                    _logger.LogDebug($"LRU expired (evicted) key={Preview(cacheKey)}");
                    return Task.FromResult<string?>(null);
                }

                // promote
                _lru.Remove(node);
                _lru.AddFirst(node);
                _logger.LogDebug($"LRU hit key={Preview(cacheKey)}");
                return Task.FromResult<string?>(node.Value.Value);
            }
        }
        _logger.LogDebug($"LRU miss key={Preview(cacheKey)}");
        return Task.FromResult<string?>(null);
    }

    public Task SetCachedResponseAsync(string cacheKey, string response, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrEmpty(response))
            return Task.CompletedTask;

        var now = DateTimeOffset.UtcNow;
        var ttl = expiration ?? _defaultTtl;
        var expiresAt = now + ttl;

        lock (_lock)
        {
            if (_map.TryGetValue(cacheKey, out var existing))
            {
                existing.Value.Value = response;
                existing.Value.ExpiresAt = expiresAt;
                _lru.Remove(existing);
                _lru.AddFirst(existing);
                _logger.LogDebug($"LRU updated key={Preview(cacheKey)} ttl={ttl.TotalMinutes:F0}m");
            }
            else
            {
                // ensure capacity (evict from tail)
                while (_map.Count >= _capacity && _lru.Last is not null)
                {
                    var tail = _lru.Last;
                    RemoveNode(tail);
                    _logger.LogDebug($"LRU evicted key={Preview(tail!.Value.Key)} (capacity)");
                }

                var entry = new Entry { Key = cacheKey, Value = response, ExpiresAt = expiresAt };
                var node = new LinkedListNode<Entry>(entry);
                _lru.AddFirst(node);
                _map[cacheKey] = node;
                _logger.LogDebug($"LRU added key={Preview(cacheKey)} ttl={ttl.TotalMinutes:F0}m size={_map.Count}/{_capacity}");
            }
        }

        return Task.CompletedTask;
    }

    public void RemoveCachedResponse(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return;
        lock (_lock)
        {
            if (_map.TryGetValue(cacheKey, out var node))
            {
                RemoveNode(node);
                _logger.LogDebug($"LRU removed key={Preview(cacheKey)}");
            }
        }
    }

    public string GenerateCacheKey(string input, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        try
        {
            var combined = context != null ? $"{context}:{input}" : input;
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError($"LRU GenerateCacheKey error: {ex.Message}");
            return string.Empty;
        }
    }

    private static bool IsExpired(Entry e) => DateTimeOffset.UtcNow >= e.ExpiresAt;

    private void RemoveNode(LinkedListNode<Entry> node)
    {
        _lru.Remove(node);
        _map.Remove(node.Value.Key);
    }

    private static string Preview(string key) => string.IsNullOrEmpty(key) ? "<empty>" : (key.Length <= 12 ? key : key[..12]) + "...";
}
