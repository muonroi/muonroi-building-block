using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Unified cache service integrated with Muonroi ecosystem (Tenancy, JSON, License, Telemetry).
/// </summary>
public interface IMCacheService
{
    /// <summary>
    /// Gets a cached value from the distributed cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached value or default.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken token = default);

    /// <summary>
    /// Sets a cached value in the distributed cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="options">Optional cache entry options.</param>
    /// <param name="token">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken token = default);

    /// <summary>
    /// Removes a cached value from the distributed cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Refreshes a cached value in the distributed cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    Task RefreshAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Gets a cached value or computes and stores it in the distributed cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="factory">Factory to create the value when missing.</param>
    /// <param name="options">Optional cache entry options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached or computed value.</returns>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken token = default) where T : class;
}
