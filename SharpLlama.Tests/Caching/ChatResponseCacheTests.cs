using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SharpLlama.ChatService;
using SharpLlama.Contracts;
using Xunit;

namespace SharpLlama.Tests.Caching;

public class ChatResponseCacheTests
{
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly Mock<ILoggerManager> _logger = new();

    [Fact]
    public async Task Set_And_Get_Works()
    {
        var cache = new ChatResponseCache(_memoryCache, _logger.Object);
        var key = cache.GenerateCacheKey("prompt", "ctx");
        await cache.SetCachedResponseAsync(key, "response");
        var cached = await cache.GetCachedResponseAsync(key);
        cached.Should().Be("response");
    }

    [Fact]
    public async Task Get_Miss_Returns_Null()
    {
        var cache = new ChatResponseCache(_memoryCache, _logger.Object);
        var cached = await cache.GetCachedResponseAsync("missing");
        cached.Should().BeNull();
    }

    [Fact]
    public void Remove_Does_Not_Throw_For_Missing()
    {
        var cache = new ChatResponseCache(_memoryCache, _logger.Object);
        cache.Invoking(c => c.RemoveCachedResponse("nope")).Should().NotThrow();
    }
}
