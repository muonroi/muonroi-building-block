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

        await action.Should().ThrowAsync<ArgumentException>();
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

        await action.Should().ThrowAsync<InvalidOperationException>();
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
    public async Task External_Distributed_Cache_Requires_License()
    {
        ExternalDistributedCache cache = new();

        Func<Task> action = async () => await cache.SetCacheAsync("k", "v", 1);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
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

        provider.GetService<RedisClient>().Should().NotBeNull();
        provider.GetService<IDistributedCache>().Should().NotBeNull();
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

        action.Should().Throw<ArgumentNullException>();
    }
}
