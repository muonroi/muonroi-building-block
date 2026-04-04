using Muonroi.Caching.Abstractions.Distributed;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Muonroi.Bff;

/// <summary>
/// Distributed refresh token store backed by <see cref="IMCacheService"/> (Redis recommended).
/// Integrated with Muonroi ecosystem for automatic tenant-scoping and license enforcement.
/// </summary>
/// <param name="cache">The ecosystem integrated cache service.</param>
/// <param name="configuration">Optional configuration to resolve TTL.</param>
public sealed class RedisTokenStore(IMCacheService cache, IConfiguration? configuration = null) : ITokenStore
{
    private const string CacheNamespace = "bff:refresh";
    private readonly TimeSpan _ttl = ResolveTtl(configuration);

    /// <inheritdoc />
    public async Task StoreRefreshTokenAsync(string subject, string refreshToken)
    {
        MGuard.NotEmpty(subject);

        string trimmedSubject = subject.Trim();
        await cache.SetAsync(trimmedSubject, refreshToken, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl,
            KeyNamespace = CacheNamespace,
            TenantScoped = true // Ensure tokens are scoped to the current tenant
        });
    }

    /// <inheritdoc />
    public Task<string?> GetRefreshTokenAsync(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult<string?>(null);
        }

        return cache.GetAsync<string>(subject.Trim());
    }

    /// <inheritdoc />
    public Task RemoveRefreshTokenAsync(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.CompletedTask;
        }

        return cache.RemoveAsync(subject.Trim());
    }

    private static TimeSpan ResolveTtl(IConfiguration? configuration)
    {
        int minutes = configuration?.GetValue<int?>("Authentication:RefreshTokenLifetimeMinutes")
            ?? configuration?.GetValue<int?>("Bff:RefreshTokenLifetimeMinutes")
            ?? 43_200; // 30 days

        return TimeSpan.FromMinutes(Math.Max(1, minutes));
    }
}
