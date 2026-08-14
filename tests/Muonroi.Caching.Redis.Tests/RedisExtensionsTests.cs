namespace Muonroi.Caching.Redis.Tests;

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

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null, string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.DistributedCache, StringComparison.OrdinalIgnoreCase);
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

        public string GetChainToken() => string.Empty;

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

        result.Should().Be("value");
    }

    [Fact]
    public async Task GetCacheAsync_Invalid_Key_Throws()
    {
        InMemoryDistributedCache cache = new();

        Func<Task> action = async () => _ = await cache.GetCacheAsync<string>(string.Empty);

        await action.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task GetCacheAsync_Missing_Key_Returns_Null()
    {
        InMemoryDistributedCache cache = new();

        string? result = await cache.GetCacheAsync<string>("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCacheAsync_Propagates_Backend_Exception()
    {
        ThrowingDistributedCache cache = new();

        Func<Task> action = async () => _ = await cache.GetCacheAsync<string>("k");

        await action.Should().ThrowAsync<MInternalException>();
    }

    [Fact]
    public async Task GetCacheAsync_NonGeneric_Returns_String_Value()
    {
        InMemoryDistributedCache cache = new();

        await cache.SetCacheAsync("plain", "text-value", 1);
        string? result = await cache.GetCacheAsync("plain");

        result.Should().Be("\"text-value\"");
    }

    [Fact]
    public async Task SetCacheAsync_WithNullExpiration_StillStoresValue()
    {
        InMemoryDistributedCache cache = new();

        await cache.SetCacheAsync("key-null-exp", "value", absoluteExpirationInMinutes: null);
        string? result = await cache.GetCacheAsync<string>("key-null-exp");

        result.Should().Be("value");
    }

    [Fact]
    public async Task RemoveAsync_Removes_Existing_Value()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("remove-key", "to-delete");

        await cache.RemoveAsync("remove-key");
        string? result = await cache.GetCacheAsync<string>("remove-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_WithInMemoryCache_Completes()
    {
        InMemoryDistributedCache cache = new();

        Func<Task> action = async () => await cache.RefreshAsync("refresh-key");

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetOrSetAsync_Uses_Cached_Value_When_Available()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("k", "cached");
        int calls = 0;

        Task<string?> Factory()
        {
            calls++;
            return Task.FromResult<string?>("new");
        }

        string? result = await cache.GetOrSetAsync("k", Factory);

        result.Should().Be("cached");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_Caches_Factory_Value_When_Missing()
    {
        InMemoryDistributedCache cache = new();
        int calls = 0;

        Task<string?> Factory()
        {
            calls++;
            return Task.FromResult<string?>("value");
        }

        string? result = await cache.GetOrSetAsync("new-key", Factory);
        string? cached = await cache.GetCacheAsync<string>("new-key");

        result.Should().Be("value");
        cached.Should().Be("value");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenFactoryReturnsNull_ReturnsNull_WithoutCaching()
    {
        InMemoryDistributedCache cache = new();

        string? result = await cache.GetOrSetAsync("null-key", () => Task.FromResult<string?>(null));
        string? cached = await cache.GetCacheAsync<string>("null-key");

        result.Should().BeNull();
        cached.Should().BeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCachedPayloadIsWhitespace_UsesFactory()
    {
        InMemoryDistributedCache cache = new();
        await cache.SetAsync(
            DistributedCacheKeyBuilder.Build("whitespace-key"),
            Encoding.UTF8.GetBytes("   "),
            new DistributedCacheEntryOptions(),
            CancellationToken.None);

        string? result = await cache.GetOrSetAsync("whitespace-key", () => Task.FromResult<string?>("fresh"));

        result.Should().Be("fresh");
    }

    [Fact]
    public async Task GetOrSetAsync_NullFactory_Throws()
    {
        InMemoryDistributedCache cache = new();

        Func<Task> action = async () => await cache.GetOrSetAsync<string>("bad-factory", null!);

        await action.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task External_Distributed_Cache_Requires_License()
    {
        ExternalDistributedCache cache = new();

        Func<Task> action = async () => await cache.SetCacheAsync("k", "v", 1);

        var exception = await action.Should().ThrowAsync<MInternalException>();
        exception.Which.Message.Should().Contain("distributed-cache");
    }

    [Fact]
    public async Task External_Distributed_Cache_Allows_Licensed_State()
    {
        ExternalDistributedCache cache = new();

        await cache.SetCacheAsync("k", "v", 1, licenseState: DistributedCacheLicensed);
        string? value = await cache.GetCacheAsync<string>("k", licenseState: DistributedCacheLicensed);

        value.Should().Be("v");
    }

    [Fact]
    public async Task External_Distributed_Cache_Guard_Takes_Precedence()
    {
        ExternalDistributedCache cache = new();

        Func<Task> action = async () => await cache.SetCacheAsync(
            "k",
            "v",
            1,
            licenseState: DistributedCacheLicensed,
            licenseGuard: new DenyDistributedCacheGuard());

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("blocked by guard");
    }

    [Fact]
    public async Task RefreshAsync_Propagates_Backend_Exception()
    {
        OperationThrowingDistributedCache cache = new(throwOnRefresh: true);

        Func<Task> action = async () => await cache.RefreshAsync("k", licenseState: DistributedCacheLicensed);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("refresh failed");
    }

    [Fact]
    public async Task RemoveAsync_Propagates_Backend_Exception()
    {
        OperationThrowingDistributedCache cache = new(throwOnRemove: true);

        Func<Task> action = async () => await cache.RemoveAsync("k", licenseState: DistributedCacheLicensed);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("remove failed");
    }

    [Fact]
    public void AddRedis_Registers_Services()
    {
        ServiceCollection services = [];
        services.AddSingleton(DistributedCacheLicensed);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        RedisConfigs configs = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd",
            Enable = true
        };

        services.AddRedis(configuration, configs);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IDistributedCache>().Should().NotBeNull();
    }

    [Fact]
    public void AddRedis_WhenDisabled_DoesNotRegisterRedisServices()
    {
        ServiceCollection services = [];
        services.AddSingleton(DistributedCacheLicensed);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        RedisConfigs configs = new()
        {
            Host = "localhost",
            Port = "6379",
            Enable = false
        };

        IServiceCollection returned = services.AddRedis(configuration, configs);
        using ServiceProvider provider = services.BuildServiceProvider();

        returned.Should().BeSameAs(services);
        provider.GetService<IDistributedCache>().Should().BeNull();
    }

    [Fact]
    public void AddRedis_UsesConfigurationOverrides_AndSupportsPasswordlessRedis()
    {
        ServiceCollection services = [];
        services.AddSingleton(DistributedCacheLicensed);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RedisConfigs.DefaultSectionName}:Host"] = "cache.internal",
                [$"{RedisConfigs.DefaultSectionName}:Port"] = "6380",
                [$"{RedisConfigs.DefaultSectionName}:Password"] = "",
                [$"{RedisConfigs.DefaultSectionName}:KeyPrefix"] = "tenant-cache"
            })
            .Build();
        RedisConfigs configs = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd",
            KeyPrefix = "old-prefix",
            Enable = true
        };

        services.AddRedis(configuration, configs);
        using ServiceProvider provider = services.BuildServiceProvider();

        configs.Host.Should().Be("cache.internal");
        configs.Port.Should().Be("6380");
        configs.Password.Should().Be("pwd");
        configs.KeyPrefix.Should().Be("tenant-cache");
    }

    [Fact]
    public void AddRedis_Throws_WhenHostOrPortMissing()
    {
        ServiceCollection services = [];
        services.AddSingleton(DistributedCacheLicensed);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        RedisConfigs configs = new()
        {
            Host = "",
            Port = "6379",
            Enable = true
        };

        Action action = () => services.AddRedis(configuration, configs);

        action.Should().Throw<MConfigurationException>()
            .WithMessage("*Host and Port are required*");
    }

    [Fact]
    public void AddRedis_Throws_When_Configuration_Is_Null()
    {
        ServiceCollection services = [];
        services.AddSingleton(DistributedCacheLicensed);
        RedisConfigs configs = new()
        {
            Host = "localhost",
            Port = "6379",
            Password = "pwd",
            Enable = true
        };

        Action action = () => services.AddRedis(null!, configs);

        action.Should().Throw<MArgumentException>();
    }

    private sealed class OperationThrowingDistributedCache(
        bool throwOnRemove = false,
        bool throwOnRefresh = false)
        : IDistributedCache
    {
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default)
            => throwOnRefresh ? throw new InvalidOperationException("refresh failed") : Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default)
            => throwOnRemove ? throw new InvalidOperationException("remove failed") : Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => Task.CompletedTask;
    }
}
