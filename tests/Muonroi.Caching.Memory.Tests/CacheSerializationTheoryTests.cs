namespace Muonroi.Caching.Memory.Tests;

public class CacheSerializationTheoryTests
{
    public class TestData
    {
        public string? Name { get; set; }

        public int Value { get; set; }
    }

    [Theory]
    [InlineData("test")]
    [InlineData("")]
    [InlineData("unicode_\u4e2d\u6587")]
    [InlineData("special@chars#123")]
    public async Task GetOrSetAsync_StringValues_SerializesCorrectly(string testValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        string? value = await service.GetOrSetAsync("string-key", () => Task.FromResult<string?>(testValue), 5);

        Assert.Equal(testValue, value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task GetOrSetAsync_IntegerValues_SerializesCorrectly(int testValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        int? value = await service.GetOrSetAsync("int-key", () => Task.FromResult<int?>(testValue), 5);

        Assert.Equal(testValue, value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetOrSetAsync_BooleanValues_SerializesCorrectly(bool testValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        bool? value = await service.GetOrSetAsync("bool-key", () => Task.FromResult<bool?>(testValue), 5);

        Assert.Equal(testValue, value);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.14)]
    [InlineData(-3.14)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public async Task GetOrSetAsync_DoubleValues_SerializesCorrectly(double testValue)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        double? value = await service.GetOrSetAsync("double-key", () => Task.FromResult<double?>(testValue), 5);

        Assert.Equal(testValue, value);
    }

    [Theory]
    [InlineData("Name1", 100)]
    [InlineData("Name2", 200)]
    [InlineData("", 0)]
    [InlineData("Test", -50)]
    public async Task GetOrSetAsync_ComplexObjects_SerializesCorrectly(string name, int value)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        TestData testData = new()
        {
            Name = name,
            Value = value
        };

        TestData? result = await service.GetOrSetAsync("object-key", () => Task.FromResult<TestData?>(testData), 5);

        Assert.NotNull(result);
        Assert.Equal(name, result.Name);
        Assert.Equal(value, result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetOrSetAsync_ListValues_SerializesCorrectly(int listSize)
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        List<string> testList = [.. Enumerable.Range(0, listSize).Select(index => $"Item{index}")];
        List<string>? result = await service.GetOrSetAsync("list-key", () => Task.FromResult<List<string>?>(testList), 5);

        Assert.NotNull(result);
        Assert.Equal(listSize, result.Count);
    }

    [Fact]
    public async Task GetOrSetAsync_EmptyList_SerializesCorrectly()
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        List<string> emptyList = [];
        List<string>? result = await service.GetOrSetAsync("empty-list", () => Task.FromResult<List<string>?>(emptyList), 5);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOrSetAsync_NullObject_DoesNotCache()
    {
        TenantContext.CurrentTenantId = null;
        InMemoryDistributedCache distributedCache = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        MultiLevelCacheService service = new(memoryCache, distributedCache);

        TestData? result = await service.GetOrSetAsync("null-object", () => Task.FromResult<TestData?>(null), 5);

        Assert.Null(result);
        Assert.False(memoryCache.TryGetValue("null-object", out _));
    }
}
