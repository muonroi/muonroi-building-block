namespace Muonroi.Core.Abstractions.Models.Common;

public static class DistributedRedisOptions
{
    public static readonly DistributedCacheEntryOptions DefaultCacheOptions105 = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };
}
