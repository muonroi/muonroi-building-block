namespace Muonroi.BuildingBlock.Test;

public class DistributedCacheTenantIsolationTests
{
    [Fact]
    public async Task MultiLevelCacheService_Isolates_ByTenant()
    {
        string? originalTenant = TenantContext.CurrentTenantId;

        try
        {
            MemoryCache memory = new(new MemoryCacheOptions());
            InMemoryDistributedCache distributed = new();
            MultiLevelCacheService service = new(memory, distributed);

            TenantContext.CurrentTenantId = "tenant-a";
            await service.SetAsync("shared-key", "value-a", 5);

            TenantContext.CurrentTenantId = "tenant-b";
            await service.SetAsync("shared-key", "value-b", 5);

            TenantContext.CurrentTenantId = "tenant-a";
            string? valueA = await service.GetAsync<string>("shared-key");

            TenantContext.CurrentTenantId = "tenant-b";
            string? valueB = await service.GetAsync<string>("shared-key");

            Assert.Equal("value-a", valueA);
            Assert.Equal("value-b", valueB);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public void DistributedCacheKeyBuilder_UsesNamespace_AndTenant()
    {
        string key = DistributedCacheKeyBuilder.Build("k", "svc-a", "tenant-1");

        Assert.Equal("svc-a:tenant-1:k", key);
    }

    [Fact]
    public async Task RedisExtensions_Isolates_ByTenant()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            InMemoryDistributedCache cache = new();

            TenantContext.CurrentTenantId = "tenant-a";
            await cache.SetCacheAsync("shared-key", "value-a", 5);

            TenantContext.CurrentTenantId = "tenant-b";
            await cache.SetCacheAsync("shared-key", "value-b", 5);

            TenantContext.CurrentTenantId = "tenant-a";
            string? valueA = await cache.GetCacheAsync<string>("shared-key");

            TenantContext.CurrentTenantId = "tenant-b";
            string? valueB = await cache.GetCacheAsync<string>("shared-key");

            Assert.Equal("value-a", valueA);
            Assert.Equal("value-b", valueB);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}
