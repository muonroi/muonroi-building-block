using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Muonroi.Caching.Abstractions.Distributed;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Bff.Tests;

public class InMemoryTokenStoreTests
{
    [Fact]
    public async Task StoreGetAndRemoveRefreshToken_ShouldRoundTrip()
    {
        InMemoryTokenStore store = new();

        await store.StoreRefreshTokenAsync("subject-a", "refresh-token");
        string? stored = await store.GetRefreshTokenAsync("subject-a");
        await store.RemoveRefreshTokenAsync("subject-a");
        string? removed = await store.GetRefreshTokenAsync("subject-a");

        Assert.Equal("refresh-token", stored);
        Assert.Null(removed);
    }
}

public class RedisTokenStoreTests
{
    [Fact]
    public async Task StoreRefreshTokenAsync_ShouldUseConfiguredTtlAndTrimmedKey()
    {
        TestCacheService cache = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:RefreshTokenLifetimeMinutes"] = "5"
            })
            .Build();
        RedisTokenStore store = new(cache, configuration);

        await store.StoreRefreshTokenAsync("  subject-a  ", "token-1");

        // The RedisTokenStore implementation passes "subject-a" as key and "bff:refresh" as namespace
        // Our TestCacheService joins them as "bff:refresh:subject-a"
        Assert.True(cache.Values.ContainsKey("bff:refresh:subject-a"), $"Key not found. Available keys: {string.Join(", ", cache.Values.Keys)}");
        Assert.Equal("token-1", cache.Values["bff:refresh:subject-a"]);
        Assert.Equal("bff:refresh", cache.LastOptions?.KeyNamespace);
        Assert.Equal(TimeSpan.FromMinutes(5), cache.LastOptions?.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task GetAndRemoveRefreshTokenAsync_ShouldHandleBlankSubjects()
    {
        TestCacheService cache = new();
        RedisTokenStore store = new(cache);

        string? missing = await store.GetRefreshTokenAsync(" ");
        await store.RemoveRefreshTokenAsync(" ");

        Assert.Null(missing);
        Assert.Null(cache.LastRemovedKey);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WithBlankSubject_ShouldThrow()
    {
        RedisTokenStore store = new(new TestCacheService());

        await Assert.ThrowsAsync<MArgumentException>(() => store.StoreRefreshTokenAsync(" ", "token"));
    }

    private sealed class TestCacheService : IMCacheService
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
        public CacheEntryOptions? LastOptions { get; private set; }
        public string? LastRemovedKey { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
        {
            if (Values.TryGetValue(key, out var value)) return Task.FromResult((T?)value);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken token = default)
        {
            LastOptions = options;
            string finalKey = string.IsNullOrEmpty(options?.KeyNamespace) ? key : $"{options.KeyNamespace}:{key}";
            Values[finalKey] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            LastRemovedKey = key;
            Values.Remove(key);
            return Task.CompletedTask;
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken token = default) where T : class
        {
            return factory();
        }
    }
}
