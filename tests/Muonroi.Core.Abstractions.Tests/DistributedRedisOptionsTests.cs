namespace Muonroi.Core.Abstractions.Tests;

public class DistributedRedisOptionsTests
{
    [Fact]
    public void DefaultCacheOptions_Are_Initialized()
    {
        DistributedCacheEntryOptions options = DistributedRedisOptions.DefaultCacheOptions105;

        options.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(10));
        options.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void DefaultCacheOptions_Return_Same_Instance_Across_Reads()
    {
        DistributedCacheEntryOptions[] values = new DistributedCacheEntryOptions[5];

        Parallel.For(0, values.Length, i => values[i] = DistributedRedisOptions.DefaultCacheOptions105);

        values.Should().OnlyContain(x => ReferenceEquals(x, DistributedRedisOptions.DefaultCacheOptions105));
    }
}
