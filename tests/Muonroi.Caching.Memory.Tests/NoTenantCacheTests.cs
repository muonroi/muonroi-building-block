namespace Muonroi.Caching.Memory.Tests;

public class NoTenantCacheTests
{
    #region DistributedCacheKeyBuilder Tests

    [Fact]
    public void Build_NullTenant_NullNamespace_Returns_PlainKey()
    {
        string result = DistributedCacheKeyBuilder.Build("mykey", null, null);

        Assert.Equal("mykey", result);
    }

    [Fact]
    public void Build_NullTenant_WithNamespace_Returns_NamespacePrefixed()
    {
        string result = DistributedCacheKeyBuilder.Build("mykey", "ns", null);

        Assert.Equal("ns:mykey", result);
    }

    [Fact]
    public void Build_WithTenant_NullNamespace_Returns_TenantPrefixed()
    {
        string result = DistributedCacheKeyBuilder.Build("mykey", null, "tenant-1");

        Assert.Equal("tenant-1:mykey", result);
    }

    [Fact]
    public void Build_WithTenant_WithNamespace_Returns_FullPrefix()
    {
        string result = DistributedCacheKeyBuilder.Build("mykey", "ns", "tenant-1");

        Assert.Equal("ns:tenant-1:mykey", result);
    }

    #endregion

    #region MultiLevelCacheService No-Tenant Integration Tests

    [Fact]
    public async Task GetOrSetAsync_NoTenant_Caches_And_Retrieves_Without_Exception()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache distributed = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, distributed);

            // Act
            string? value = await service.GetOrSetAsync("testkey",
                () => Task.FromResult<string?>("hello"), 1);

            // Assert: set works without tenant
            Assert.Equal("hello", value);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_NoTenant_Value_Retrievable_Via_GetAsync()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache distributed = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, distributed);

            await service.GetOrSetAsync("testkey",
                () => Task.FromResult<string?>("hello"), 1);

            // Act: retrieve via GetAsync
            string? retrieved = await service.GetAsync<string>("testkey");

            // Assert
            Assert.Equal("hello", retrieved);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_WithTenant_Uses_TenantPrefixedKey()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = "tenant-abc";

            InMemoryDistributedCache distributed = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, distributed);

            // Act
            string? value = await service.GetOrSetAsync("testkey",
                () => Task.FromResult<string?>("tenant-value"), 1);

            // Assert: tenant-prefixed key in memory
            Assert.Equal("tenant-value", value);
            Assert.True(memory.TryGetValue("tenant-abc:testkey", out string? cached));
            Assert.Equal("tenant-value", cached);

            // Non-prefixed key should NOT exist
            Assert.False(memory.TryGetValue("testkey", out _));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_NoTenant_Uses_NonPrefixedKey()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = null;

            InMemoryDistributedCache distributed = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, distributed);

            // Act
            string? value = await service.GetOrSetAsync("testkey",
                () => Task.FromResult<string?>("no-tenant-value"), 1);

            // Assert: non-prefixed key in memory
            Assert.Equal("no-tenant-value", value);
            Assert.True(memory.TryGetValue("testkey", out string? cached));
            Assert.Equal("no-tenant-value", cached);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    #endregion
}
