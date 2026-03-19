using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace Muonroi.Bff;

/// <summary>
/// Distributed refresh token store backed by IDistributedCache (Redis recommended).
/// </summary>
/// <param name="cache">The distributed cache to store tokens in.</param>
/// <param name="configuration">Optional configuration to resolve TTL.</param>
public sealed class RedisTokenStore(IDistributedCache cache, IConfiguration? configuration = null) : ITokenStore
{
    private readonly TimeSpan _ttl = ResolveTtl(configuration);

    /// <inheritdoc />
    public Task StoreRefreshTokenAsync(string subject, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.", nameof(subject));
        }

        string key = BuildKey(subject);
        return cache.SetStringAsync(key, refreshToken, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl
        });
    }

    /// <inheritdoc />
    public Task<string?> GetRefreshTokenAsync(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult<string?>(null);
        }

        string key = BuildKey(subject);
        return cache.GetStringAsync(key);
    }

    /// <inheritdoc />
    public Task RemoveRefreshTokenAsync(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.CompletedTask;
        }

        string key = BuildKey(subject);
        return cache.RemoveAsync(key);
    }

    private static string BuildKey(string subject)
    {
        return $"bff:refresh:{subject.Trim()}";
    }

    private static TimeSpan ResolveTtl(IConfiguration? configuration)
    {
        int minutes = configuration?.GetValue<int?>("Authentication:RefreshTokenLifetimeMinutes")
            ?? configuration?.GetValue<int?>("Bff:RefreshTokenLifetimeMinutes")
            ?? 43_200; // 30 days

        return TimeSpan.FromMinutes(Math.Max(1, minutes));
    }
}
