using Microsoft.Extensions.Caching.Memory;
using Muonroi.Caching.Abstractions.Distributed;

namespace Quickstart.AuthZ.Api.Infrastructure;

/// <summary>
/// Minimal in-process <see cref="IMCacheService"/> backed by IMemoryCache.
///
/// RuleEngineAuthorizationPolicyEvaluator (the IAuthorizationPolicyEvaluator
/// registered by AddMAuthorizationRuleEngine) depends on IMCacheService to
/// short-circuit repeated decisions. The shipped implementation is
/// RedisCacheService (Muonroi.Caching.Redis), which needs a Redis server.
/// This sample registers this in-memory implementation so the package runs
/// with no external dependency. In production register AddRedisCache() instead.
/// </summary>
public sealed class InMemoryCacheService(IMemoryCache cache) : IMCacheService
{
    private static string Compose(string key, CacheEntryOptions? options)
    {
        string ns = options?.KeyNamespace ?? "default";
        return $"{ns}:{key}";
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        // The evaluator reads with no options and writes with KeyNamespace set, so
        // this lookup intentionally misses and rules are re-evaluated each call —
        // the safe default for a demo. A production IMCacheService (RedisCacheService)
        // resolves the namespace consistently for both read and write.
        cache.TryGetValue(Compose(key, null), out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken token = default)
    {
        MemoryCacheEntryOptions entryOptions = new();
        if (options?.AbsoluteExpirationRelativeToNow is { } abs)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = abs;
        }
        if (options?.SlidingExpiration is { } sliding)
        {
            entryOptions.SlidingExpiration = sliding;
        }

        cache.Set(Compose(key, options), value, entryOptions);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        cache.Remove(Compose(key, null));
        return Task.CompletedTask;
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        // IMemoryCache has no explicit refresh; sliding expiry refreshes on access.
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory,
        CacheEntryOptions? options = null, CancellationToken token = default) where T : class
    {
        if (cache.TryGetValue(Compose(key, options), out T? existing) && existing is not null)
        {
            return existing;
        }

        T? created = await factory();
        if (created is not null)
        {
            await SetAsync(key, created, options, token);
        }
        return created;
    }
}
