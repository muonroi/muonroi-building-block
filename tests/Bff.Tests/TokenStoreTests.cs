using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

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
        TestDistributedCache cache = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:RefreshTokenLifetimeMinutes"] = "5"
            })
            .Build();
        RedisTokenStore store = new(cache, configuration);

        await store.StoreRefreshTokenAsync("  subject-a  ", "token-1");

        Assert.Equal("token-1", cache.Values["bff:refresh:subject-a"]);
        Assert.Equal(TimeSpan.FromMinutes(5), cache.LastOptions?.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task GetAndRemoveRefreshTokenAsync_ShouldHandleBlankSubjects()
    {
        TestDistributedCache cache = new();
        RedisTokenStore store = new(cache);

        string? missing = await store.GetRefreshTokenAsync(" ");
        await store.RemoveRefreshTokenAsync(" ");

        Assert.Null(missing);
        Assert.Null(cache.LastRemovedKey);
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WithBlankSubject_ShouldThrow()
    {
        RedisTokenStore store = new(new TestDistributedCache());

        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreRefreshTokenAsync(" ", "token"));
    }

    private sealed class TestDistributedCache : IDistributedCache
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public DistributedCacheEntryOptions? LastOptions { get; private set; }
        public string? LastRemovedKey { get; private set; }

        public byte[]? Get(string key)
        {
            return Values.TryGetValue(key, out string? value) ? System.Text.Encoding.UTF8.GetBytes(value) : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            LastRemovedKey = key;
            Values.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Values[key] = System.Text.Encoding.UTF8.GetString(value);
            LastOptions = options;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
