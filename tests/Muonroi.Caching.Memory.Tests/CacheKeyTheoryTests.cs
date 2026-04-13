namespace Muonroi.Caching.Memory.Tests;

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
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>("value"), 1);

        Assert.Equal("value", value);
        Assert.True(memoryCache.TryGetValue(key, out string? cached));
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
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>("path-value"), 1);

        Assert.Equal("path-value", value);
    }

    [Theory]
    [InlineData("unicode_\u4e2d\u6587")]
    [InlineData("unicode_\u65e5\u672c\u8a9e")]
    [InlineData("unicode_\ud55c\uae00")]
    [InlineData("unicode_\u0627\u0644\u0639\u0631\u0628\u064a\u0629")]
    [InlineData("emoji_\ud83d\ude00\ud83c\udf89")]
    public async Task GetOrSetAsync_UnicodeKeys_HandlesCorrectly(string key)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

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
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

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
            InMemoryDistributedCache distributedCache = new();
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memoryCache, distributedCache);

            string? value = await service.GetOrSetAsync("key", () => Task.FromResult<string?>("tenant-value"), 1);

            Assert.Equal("tenant-value", value);
            Assert.True(memoryCache.TryGetValue($"{tenantId}:key", out string? cached));
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
            InMemoryDistributedCache distributedCache = new();
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memoryCache, distributedCache);

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
            InMemoryDistributedCache distributedCache = new();
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MultiLevelCacheService service = new(memoryCache, distributedCache);

            string? value = await service.GetOrSetAsync("key", () => Task.FromResult<string?>("no-tenant"), 1);

            Assert.Equal("no-tenant", value);
            Assert.True(memoryCache.TryGetValue("key", out string? cached));
            Assert.Equal("no-tenant", cached);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}
