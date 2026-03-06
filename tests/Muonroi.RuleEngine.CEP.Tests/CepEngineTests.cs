namespace Muonroi.RuleEngine.CEP.Tests;

public class CepEngineTests
{
    [Fact]
    public void SlidingWindow_ReturnsEventsWithinWindow()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = DateTime.UtcNow;
        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 2, t0.AddSeconds(15));
        Assert.Single(result);
        Assert.Equal(2, result[0].Value);
    }

    [Fact]
    public void TumblingWindow_GroupsIntoBuckets()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Tumbling);
        DateTime t0 = DateTime.UtcNow;
        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> bucket2 = engine.AddEvent("a", 2, t0.AddSeconds(15));
        Assert.Single(bucket2);
        Assert.Equal(2, bucket2[0].Value);
    }

    [Fact]
    public void Ttl_RemovesExpiredEvents()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding, ttl: TimeSpan.FromSeconds(10));
        DateTime t0 = DateTime.UtcNow;
        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 2, t0.AddSeconds(25));
        Assert.Single(result);
        Assert.Equal(2, result[0].Value);
    }

    [Fact]
    public void OutOfOrder_IsSortedByTimestamp()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = DateTime.UtcNow;
        engine.AddEvent("a", 1, t0.AddSeconds(5));
        engine.AddEvent("a", 2, t0); // arrives late
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 3, t0.AddSeconds(6));
        Assert.Equal(3, result.Count);
        Assert.True(result[0].Timestamp <= result[1].Timestamp && result[1].Timestamp <= result[2].Timestamp);
    }

    [Fact]
    public void Correlation_ByKeySeparatesEvents()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = DateTime.UtcNow;
        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> resultB = engine.AddEvent("b", 2, t0.AddSeconds(5));
        Assert.Single(resultB);
        Assert.Equal("b", resultB[0].Key);
    }
}