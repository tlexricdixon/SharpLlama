using ChatService;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace SharpLlama.Tests.Metrics;

public class ChatMetricsTests
{
    private readonly ChatMetrics _metrics;
    private readonly Mock<ILogger<ChatMetrics>> _logger = new();

    public ChatMetricsTests()
    {
        _metrics = new ChatMetrics(_logger.Object);
    }

    [Fact]
    public void RequestCount_Increments()
    {
        _metrics.IncrementRequestCount("Stateful");
        _metrics.IncrementRequestCount("Stateful");
        var snapshot = _metrics.GetSnapshot();
        snapshot.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void Records_Response_Time_And_Length()
    {
        _metrics.RecordResponseTime("Stateful", TimeSpan.FromMilliseconds(100));
        _metrics.RecordResponseLength("Stateful", 50);
        var snapshot = _metrics.GetSnapshot();
        snapshot.AverageResponseTimeMs.Should().BeGreaterThan(0);
        snapshot.AverageResponseLength.Should().Be(50);
    }

    [Fact]
    public void Cache_Hits_And_Misses_Increment()
    {
        _metrics.IncrementCacheHit("Stateful");
        _metrics.IncrementCacheMiss("Stateful");
        var snap = _metrics.GetSnapshot();
        snap.CacheHits.Should().Be(1);
        snap.CacheMisses.Should().Be(1);
    }
}
