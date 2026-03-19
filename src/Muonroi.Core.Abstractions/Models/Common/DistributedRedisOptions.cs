namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Provides default options for distributed Redis caching. </summary>
public static class DistributedRedisOptions
{
    /// <summary> Gets the default cache entry options with 10 minutes absolute and 5 minutes sliding expiration. </summary>
    public static readonly DistributedCacheEntryOptions DefaultCacheOptions105 = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };
}
