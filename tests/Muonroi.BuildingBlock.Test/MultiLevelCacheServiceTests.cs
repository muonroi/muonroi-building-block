using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Tenancy;
using Xunit;

namespace Muonroi.BuildingBlock.Test;

public class MultiLevelCacheServiceTests
{
    [Fact]
    public async Task GetOrSetAsync_Returns_Distributed_Value_When_Not_In_Memory()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            // prepopulate distributed cache with serialized value
            await dist.SetAsync("key", Encoding.UTF8.GetBytes("\"cached\""), new DistributedCacheEntryOptions());
            int factoryCalls = 0;

            Task<string?> Factory()
            {
                factoryCalls++;
                return Task.FromResult<string?>("factory");
            }

            string? value = await service.GetOrSetAsync("key", Factory, 1);

            Assert.Equal("cached", value);
            Assert.Equal(0, factoryCalls);
            Assert.True(memory.TryGetValue("key", out string? memVal));
            Assert.Equal("cached", memVal);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_Factory_Returns_Null_Does_Not_Cache()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            string? value = await service.GetOrSetAsync("null-key", () => Task.FromResult<string?>(null), 1);

            Assert.Null(value);
            Assert.False(memory.TryGetValue("null-key", out _));
            Assert.Null(dist.Get("null-key"));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_Returns_Value_From_Memory_When_Available()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            memory.Set("mem-key", "mem");
            int calls = 0;

            Task<string?> Factory()
            {
                calls++;
                return Task.FromResult<string?>("f");
            }

            string? value = await service.GetOrSetAsync("mem-key", Factory, 1);

            Assert.Equal("mem", value);
            Assert.Equal(0, calls);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_Key_Exists_In_Distributed_Caches_Memory()
    {
        const string tenantId = "tenant";
        TenantContext.CurrentTenantId = tenantId;
        try
        {
            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            await dist.SetAsync($"{tenantId}:dist-key", Encoding.UTF8.GetBytes("\"dval\""),
                new DistributedCacheEntryOptions());
            int calls = 0;

            Task<string?> Factory()
            {
                calls++;
                return Task.FromResult<string?>("f");
            }

            string? value = await service.GetOrSetAsync("dist-key", Factory, 1);

            Assert.Equal("dval", value);
            Assert.Equal(0, calls);
            Assert.True(memory.TryGetValue($"{tenantId}:dist-key", out string? memVal));
            Assert.Equal("dval", memVal);
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_Key_Missing_Caches_Factory_Value()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            int calls = 0;

            Task<string?> Factory()
            {
                calls++;
                return Task.FromResult<string?>("new");
            }

            string? value = await service.GetOrSetAsync("new-key", Factory, 1);

            Assert.Equal("new", value);
            Assert.Equal(1, calls);
            Assert.True(memory.TryGetValue("new-key", out string? memVal));
            Assert.Equal("new", memVal);
            byte[]? bytes = dist.Get("new-key");
            Assert.NotNull(bytes);
            Assert.Equal("\"new\"", Encoding.UTF8.GetString(bytes!));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task SetAsync_Stores_Value_In_Both_Caches_With_Tenant_Prefix()
    {
        const string tenantId = "tenant-set";
        TenantContext.CurrentTenantId = tenantId;
        try
        {
            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            await service.SetAsync("set-key", "value", 1);

            Assert.True(memory.TryGetValue($"{tenantId}:set-key", out string? memVal));
            Assert.Equal("value", memVal);

            byte[]? bytes = dist.Get($"{tenantId}:set-key");
            Assert.NotNull(bytes);
            Assert.Equal("\"value\"", Encoding.UTF8.GetString(bytes!));
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }

    [Fact]
    public async Task SetAsync_Uses_Configured_KeyNamespace()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;
            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(
                memory,
                dist,
                cacheConfigs: new CacheConfigs { KeyNamespace = "svc-a" });

            await service.SetAsync("set-key", "value", 1);

            Assert.True(memory.TryGetValue("svc-a:set-key", out string? memVal));
            Assert.Equal("value", memVal);
            byte[]? bytes = dist.Get("svc-a:set-key");
            Assert.NotNull(bytes);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_Key_Null_Throws()
    {
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, dist);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.GetOrSetAsync(null!, () => Task.FromResult<string?>("v")));
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentRequests_CallFactoryOnce()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache dist = new();
            IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);
            int factoryCalls = 0;

            Task<string?> Factory()
            {
                Interlocked.Increment(ref factoryCalls);
                return Task.FromResult<string?>("value");
            }

            Task<string?>[] tasks = [.. Enumerable.Range(0, 12).Select(_ => service.GetOrSetAsync("stampede-key", Factory, 1))];
            string?[] results = await Task.WhenAll(tasks);

            Assert.All(results, v => Assert.Equal("value", v));
            Assert.Equal(1, factoryCalls);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}
