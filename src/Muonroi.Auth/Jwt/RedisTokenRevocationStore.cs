using Microsoft.Extensions.Caching.Distributed;

namespace Muonroi.Auth.Jwt;

public sealed class RedisTokenRevocationStore(IDistributedCache cache, IMDateTimeService dateTimeService) : ITokenRevocationStore
{
    public void Revoke(string jti, DateTime expires)
    {
        if (string.IsNullOrWhiteSpace(jti) || expires <= dateTimeService.UtcNow())
        {
            return;
        }

        cache.SetString(
            BuildKey(jti),
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expires
            });
    }

    public bool IsRevoked(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        return cache.Get(BuildKey(jti)) is not null;
    }

    private static string BuildKey(string jti) => $"revoked:{jti}";
}
