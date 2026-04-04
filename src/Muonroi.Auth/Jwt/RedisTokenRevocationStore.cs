using Microsoft.Extensions.Caching.Distributed;

namespace Muonroi.Auth.Jwt;

/// <summary>
/// A Redis-backed implementation of the ITokenRevocationStore.
/// </summary>
/// <param name="cache">The distributed cache for storage.</param>
/// <param name="dateTimeService">Service to retrieve the current date and time.</param>
public sealed class RedisTokenRevocationStore(IDistributedCache cache, IMDateTimeService dateTimeService) : ITokenRevocationStore
{
    /// <summary>
    /// Revokes a JWT identifier by storing it in Redis until its expiration time.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to revoke.</param>
    /// <param name="expires">The date and time when the token expires.</param>
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

    /// <summary>
    /// Checks if a JWT identifier has been revoked by looking it up in Redis.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to check.</param>
    /// <returns>True if the token is revoked; otherwise, false.</returns>
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
