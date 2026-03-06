using FreeRedis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Muonroi.Caching.Redis.Redis;
using System.Text;
using System.Text.Json;

namespace Muonroi.BuildingBlock.Test;

public class InMemoryDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = [];

    public byte[]? Get(string key)
    {
        return _store.TryGetValue(key, out byte[]? v) ? v : null;
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
        _store.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        _ = _store.Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _store[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}

public class ExternalDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = [];

    public byte[]? Get(string key)
    {
        return _store.TryGetValue(key, out byte[]? v) ? v : null;
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
        _store.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        _ = _store.Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _store[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}

public class RedisExtensionsTests
{
    private static readonly LicenseState DistributedCacheLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.DistributedCache]
    };

    private sealed class DenyDistributedCacheGuard : ILicenseGuard
    {
        public LicenseState Current => DistributedCacheLicensed;
        public LicenseTier Tier => LicenseTier.Licensed;
        public bool IsFreeMode => false;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.DistributedCache,
                StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new InvalidOperationException("distributed-cache feature blocked by guard");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return string.Empty;
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return encryptedData;
        }
    }

    public RedisExtensionsTests()
    {
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task Set_And_Get_String_Value()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("key", "value", 1);
        string? result = await cache.GetCacheAsync<string>("key");
        Assert.Equal("value", result);
    }

    [Fact]
    public async Task GetCacheAsync_InvalidKey_Throws()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetCacheAsync<string>(""));
    }

    [Fact]
    public async Task GetOrSetAsync_Returns_Cached_Value()
    {
        InMemoryDistributedCache cache = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, cache);
        int callCount = 0;

        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("data");
        }

        string? result1 = await service.GetOrSetAsync("k", Factory, 1);
        string? result2 = await service.GetOrSetAsync("k", () => Task.FromResult<string?>(null), 1);

        Assert.Equal("data", result1);
        Assert.Equal("data", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task MultiLevelCache_Set_Get_Remove_Works()
    {
        InMemoryDistributedCache cache = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, cache);

        await service.SetAsync("cache-key", "v", 1);
        string? value = await service.GetAsync<string>("cache-key");
        Assert.Equal("v", value);

        await service.RemoveAsync("cache-key");
        string? removed = await service.GetAsync<string>("cache-key");
        Assert.Null(removed);
    }

    [Fact]
    public async Task GetCacheAsync_KeyNull_Throws()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetCacheAsync<string>(null!));
    }

    [Fact]
    public async Task GetCacheAsync_KeyNotFound_ReturnsNull()
    {
        InMemoryDistributedCache cache = new();
        string? result = await cache.GetCacheAsync<string>("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCacheAsync_GetThrows_Propagates()
    {
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        cache.GetAsync("k", Arg.Any<CancellationToken>())
            .Returns<Task<byte[]?>>(_ => throw new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetCacheAsync<string>("k"));
    }

    [Fact]
    public async Task GetCacheAsync_Invalid_Type_Throws()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetAsync("k", Encoding.UTF8.GetBytes("notjson"), new DistributedCacheEntryOptions());
        await Assert.ThrowsAsync<JsonException>(() => cache.GetCacheAsync<int>("k"));
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_KeyNull_ReturnsNull()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            cache.GetOrSetAsync(null!, () => Task.FromResult<string?>("v")));
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_SetValueNull_ReturnsNull()
    {
        InMemoryDistributedCache cache = new();
        string? value = await cache.GetOrSetAsync("k", () => Task.FromResult<string?>(null));
        Assert.Null(value);
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_KeyExists_DoesNotCallFactory()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("k", "cached");
        int callCount = 0;

        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("new");
        }

        string? result = await cache.GetOrSetAsync("k", Factory);

        Assert.Equal("cached", result);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_KeyMissing_CachesFactoryValue()
    {
        InMemoryDistributedCache cache = new();
        int callCount = 0;

        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("value");
        }

        string? result = await cache.GetOrSetAsync("new-key", Factory);
        string? cached = await cache.GetCacheAsync<string>("new-key");

        Assert.Equal("value", result);
        Assert.Equal("value", cached);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_FactoryThrows_Propagates()
    {
        InMemoryDistributedCache cache = new();

        static Task<string?> Factory()
        {
            throw new InvalidOperationException();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrSetAsync("err", Factory));
        string? cached = await cache.GetCacheAsync<string>("err");

        Assert.Null(cached);
    }

    [Fact]
    public async Task Distributed_GetOrSetAsync_KeyEmpty_Throws()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.GetOrSetAsync(string.Empty, () => Task.FromResult<string?>("val")));
    }

    [Fact]
    public async Task ExternalDistributedCache_FreeMode_Throws_For_DistributedCacheFeature()
    {
        ExternalDistributedCache cache = new();
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.SetCacheAsync("k", "v", 1));
        Assert.Contains("distributed-cache", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalDistributedCache_LicensedState_Allows()
    {
        ExternalDistributedCache cache = new();

        await cache.SetCacheAsync("k", "v", 1, licenseState: DistributedCacheLicensed, cancellationToken: default);
        string? value = await cache.GetCacheAsync<string>("k", licenseState: DistributedCacheLicensed, cancellationToken: default);

        Assert.Equal("v", value);
    }

    [Fact]
    public async Task ExternalDistributedCache_LicenseGuardDenies_TakesPrecedence()
    {
        ExternalDistributedCache cache = new();
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.SetCacheAsync(
                "k",
                "v",
                1,
                licenseState: DistributedCacheLicensed,
                licenseGuard: new DenyDistributedCacheGuard(),
                cancellationToken: default));

        Assert.Contains("distributed-cache feature blocked by guard", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_KeyNull_Throws()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAsync<ArgumentNullException>(() => RedisExtensions.RefreshAsync(cache, null!));
    }

    [Fact]
    public async Task RefreshAsync_KeyNotFound_DoesNotThrow()
    {
        InMemoryDistributedCache cache = new();
        await cache.RefreshAsync("none");
        Assert.True(true);
    }

    [Fact]
    public async Task RemoveAsync_KeyNull_Throws()
    {
        InMemoryDistributedCache cache = new();
        await Assert.ThrowsAsync<ArgumentNullException>(() => RedisExtensions.RemoveAsync(cache, null!));
    }

    [Fact]
    public async Task RemoveAsync_KeyNotFound_DoesNotThrow()
    {
        InMemoryDistributedCache cache = new();
        await cache.RemoveAsync("none");
        Assert.True(true);
    }

    [Fact]
    public void AddRedis_Registers_Services()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder().Build();
        RedisConfigs cfg = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd",
            Enable = true
        };
        services.AddRedis(configuration, cfg);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<RedisClient>());
        Assert.NotNull(provider.GetService<IDistributedCache>());
    }

    [Fact]
    public void AddRedis_Duplicate()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder().Build();
        RedisConfigs cfg = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd",
            Enable = true
        };
        services.AddRedis(configuration, cfg);
        services.AddRedis(configuration, cfg);
        int count = services.Count(d => d.ServiceType == typeof(RedisClient));
        Assert.Equal(2, count);
    }

    [Fact]
    public void AddRedis_NullConfig_Throws()
    {
        ServiceCollection services = [];
        RedisConfigs cfg = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd"
        };
        Assert.Throws<ArgumentNullException>(() => services.AddRedis(null!, cfg));
    }
}
