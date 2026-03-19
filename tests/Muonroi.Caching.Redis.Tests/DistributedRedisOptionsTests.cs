namespace Muonroi.Caching.Redis.Tests;

public class DistributedRedisOptionsTests
{
    [Fact]
    public void DefaultCacheOptions_Initialized_With_Correct_Values()
    {
        DistributedCacheEntryOptions options = DistributedRedisOptions.DefaultCacheOptions105;
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(10), options.AbsoluteExpirationRelativeToNow);
        Assert.Equal(TimeSpan.FromMinutes(5), options.SlidingExpiration);
    }

    [Fact]
    public void DefaultCacheOptions_Returns_Same_Instance_MultiThread()
    {
        DistributedCacheEntryOptions[] results = new DistributedCacheEntryOptions[5];
        Parallel.For(0, results.Length, i => results[i] = DistributedRedisOptions.DefaultCacheOptions105);

        Assert.All(results, option => Assert.Same(DistributedRedisOptions.DefaultCacheOptions105, option));
    }
}
