namespace Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Cache entry options for <see cref="IMCacheService"/>.
/// </summary>
public sealed record CacheEntryOptions
{
    /// <summary>
    /// Gets or sets an absolute expiration relative to now.
    /// Default: 1440 minutes (24 hours).
    /// </summary>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; init; } = TimeSpan.FromMinutes(1440);

    /// <summary>
    /// Gets or sets how long a cache entry can be inactive (e.g., not accessed) before it will be removed.
    /// This will not extend the entry lifetime beyond the absolute expiration (if set).
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>
    /// Gets or sets a namespace to prefix the cache key.
    /// Default: null.
    /// </summary>
    public string? KeyNamespace { get; init; }

    /// <summary>
    /// Gets or sets whether to use tenant-specific scoping.
    /// Default: true (ecosystem default).
    /// </summary>
    public bool TenantScoped { get; init; } = true;
}
