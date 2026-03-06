using Microsoft.Extensions.Caching.Memory;

namespace Muonroi.BuildingBlock.Test;

public class CacheKeyTheoryTests
{
    [Theory]
    [InlineData("key@special")]
    [InlineData("key#hash")]
    [InlineData("key$dollar")]
    [InlineData("key%percent")]
    [InlineData("key&ampersand")]
    [InlineData("key*asterisk")]
    public async Task GetOrSetAsync_SpecialCharactersInKey_HandlesCorrectly(string key)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache dist = new();
        MemoryCache memory = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, dist);

        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>("value"), 1);

        Assert.Equal("value", value);
        Assert.True(memory.TryGetValue(key, out string? cached));
        Assert.Equal("value", cached);
    }

    [Theory]
    [InlineData("key:with:colons")]
    [InlineData("key/with/slashes")]
    [InlineData("key|with|pipes")]
    [InlineData("key<with>brackets")]
    public async Task GetOrSetAsync_PathLikeKeys_HandlesCorrectly(string key)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache dist = new();
        MemoryCache memory = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, dist);

        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>("path-value"), 1);

        Assert.Equal("path-value", value);
    }

    [Theory]
    [InlineData("unicode_中文")]
    [InlineData("unicode_日本語")]
    [InlineData("unicode_한글")]
    [InlineData("unicode_العربية")]
    [InlineData("emoji_😀🎉")]
    public async Task GetOrSetAsync_UnicodeKeys_HandlesCorrectly(string key)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache dist = new();
        MemoryCache memory = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, dist);

        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>("unicode"), 1);

        Assert.Equal("unicode", value);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task GetOrSetAsync_LongKeys_HandlesCorrectly(int keyLength)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache dist = new();
        MemoryCache memory = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memory, dist);

        string longKey = new('a', keyLength);
        string? value = await service.GetOrSetAsync(longKey, () => Task.FromResult<string?>("long-key-value"), 1);

        Assert.Equal("long-key-value", value);
    }

    [Theory]
    [InlineData("tenant1")]
    [InlineData("tenant-123")]
    [InlineData("tenant_abc")]
    [InlineData("TENANT_UPPER")]
    public async Task GetOrSetAsync_WithTenantPrefix_CreatesCorrectKey(string tenantId)
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = tenantId;
            InMemoryDistributedCache dist = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            string? value = await service.GetOrSetAsync("key", () => Task.FromResult<string?>("tenant-value"), 1);

            Assert.Equal("tenant-value", value);
            Assert.True(memory.TryGetValue($"{tenantId}:key", out string? cached));
            Assert.Equal("tenant-value", cached);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant@special")]
    [InlineData("tenant#123")]
    public async Task GetOrSetAsync_SpecialCharactersInTenant_HandlesCorrectly(string tenantId)
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = tenantId;
            InMemoryDistributedCache dist = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            string? value = await service.GetOrSetAsync("key", () => Task.FromResult<string?>("special-tenant"), 1);

            Assert.Equal("special-tenant", value);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task GetOrSetAsync_NullTenant_UsesKeyWithoutPrefix()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = string.Empty;
            InMemoryDistributedCache dist = new();
            MemoryCache memory = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memory, dist);

            string? value = await service.GetOrSetAsync("key", () => Task.FromResult<string?>("no-tenant"), 1);

            Assert.Equal("no-tenant", value);
            Assert.True(memory.TryGetValue("key", out string? cached));
            Assert.Equal("no-tenant", cached);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}
