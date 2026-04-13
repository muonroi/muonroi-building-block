namespace Muonroi.Caching.Memory.Tests;

public class CacheTtlTheoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(300)]
    public async Task GetOrSetAsync_PositiveTtl_CachesValue(int ttlMinutes)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("ttl-key", () => Task.FromResult<string?>("ttl-value"), ttlMinutes);

        Assert.Equal("ttl-value", value);
        Assert.True(memoryCache.TryGetValue("ttl-key", out string? cached));
        Assert.Equal("ttl-value", cached);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public async Task GetOrSetAsync_ZeroOrNegativeTtl_StillCachesValue(int ttlMinutes)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("ttl-key", () => Task.FromResult<string?>("value"), ttlMinutes);

        Assert.Equal("value", value);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(525600)]
    [InlineData(43200)]
    public async Task GetOrSetAsync_VeryLargeTtl_HandlesCorrectly(int ttlMinutes)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("large-ttl", () => Task.FromResult<string?>("large"), ttlMinutes);

        Assert.Equal("large", value);
    }

    [Theory]
    [InlineData("value1")]
    [InlineData("value2")]
    [InlineData("value3")]
    public async Task GetOrSetAsync_DifferentValues_CachesEachCorrectly(string testValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string key = $"key-{testValue}";
        string? value = await service.GetOrSetAsync(key, () => Task.FromResult<string?>(testValue), 5);

        Assert.Equal(testValue, value);
        Assert.True(memoryCache.TryGetValue(key, out string? cached));
        Assert.Equal(testValue, cached);
    }

    [Fact]
    public async Task GetOrSetAsync_NullValue_DoesNotCache()
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("null-key", () => Task.FromResult<string?>(null), 5);

        Assert.Null(value);
        Assert.False(memoryCache.TryGetValue("null-key", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task GetOrSetAsync_EmptyOrWhitespaceValue_CachesValue(string emptyValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("empty-key", () => Task.FromResult<string?>(emptyValue), 5);

        Assert.Equal(emptyValue, value);
    }
}
