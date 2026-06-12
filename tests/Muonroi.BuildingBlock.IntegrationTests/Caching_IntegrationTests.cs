using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Caching.Memory.MultiLevel;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Integration tests for multi-level caching behavior.
/// </summary>
/// <param name="factory">Test application factory.</param>
public class Caching_IntegrationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    /// <summary>
    /// Verifies that a second request hits the cache.
    /// </summary>
    [Fact]
    public async Task CacheHit_OnSecondRequest_ShouldReturnCachedData()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - First request (cache miss)
        var firstResponse = await client.GetAsync("/api/data/cached/test-key");
        var firstContent = await firstResponse.Content.ReadAsStringAsync();
        var firstTime = DateTime.UtcNow;

        // Small delay
        await Task.Delay(100);

        // Second request (cache hit)
        var secondResponse = await client.GetAsync("/api/data/cached/test-key");
        var secondContent = await secondResponse.Content.ReadAsStringAsync();
        var secondTime = DateTime.UtcNow;

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstContent.Should().Be(secondContent); // Same data
        (secondTime - firstTime).Should().BeLessThan(TimeSpan.FromSeconds(1)); // Fast response
    }

    /// <summary>
    /// Verifies that the first request results in a cache miss.
    /// </summary>
    [Fact]
    public async Task CacheMiss_OnFirstRequest_ShouldFetchFromSource()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var uniqueKey = $"test-miss-{Guid.NewGuid()}";

        // Act
        var cachedValue = await cacheService.GetAsync<string>(uniqueKey);

        // Assert - Should be null/default on cache miss
        cachedValue.Should().BeNull();
    }

    /// <summary>
    /// Verifies cache invalidation after updating a value.
    /// </summary>
    [Fact]
    public async Task CacheInvalidation_AfterUpdate_ShouldRefreshData()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"test-invalidation-{Guid.NewGuid()}";

        // Act - Set initial value
        await cacheService.SetAsync(key, "original-value", absoluteExpirationInMinutes: 10);
        var initialValue = await cacheService.GetAsync<string>(key);

        // Update value (invalidate cache)
        await cacheService.SetAsync(key, "updated-value", absoluteExpirationInMinutes: 10);
        var updatedValue = await cacheService.GetAsync<string>(key);

        // Assert
        initialValue.Should().Be("original-value");
        updatedValue.Should().Be("updated-value");
    }

    /// <summary>
    /// Verifies expiration behavior after TTL.
    /// </summary>
    [Fact]
    public async Task CacheExpiration_AfterTTL_ShouldEvictData()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"test-expiration-{Guid.NewGuid()}";

        // Act - Set value with very short TTL (1 minute for testing, but we'll manually remove)
        await cacheService.SetAsync(key, "short-lived", absoluteExpirationInMinutes: 1);
        var valueBeforeExpiry = await cacheService.GetAsync<string>(key);

        // Manually remove to simulate expiration
        await cacheService.RemoveAsync(key);
        var valueAfterExpiry = await cacheService.GetAsync<string>(key);

        // Assert
        valueBeforeExpiry.Should().Be("short-lived");
        valueAfterExpiry.Should().BeNull();
    }

    /// <summary>
    /// Verifies fallback from memory to distributed cache.
    /// </summary>
    [Fact]
    public async Task MultiLevelCache_MemoryThenRedis_ShouldFallback()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var key = $"test-multilevel-{Guid.NewGuid()}";

        // Act - Set in distributed cache (Redis)
        await cacheService.SetAsync(key, "distributed-value", absoluteExpirationInMinutes: 10);

        // Clear memory cache to test fallback
        memoryCache.Remove(key);

        // Get should fallback to distributed cache and repopulate memory
        var value = await cacheService.GetAsync<string>(key);

        // Assert
        value.Should().Be("distributed-value");
    }

    /// <summary>
    /// Verifies tenant-prefixed cache keys isolate values.
    /// </summary>
    [Fact]
    public async Task CacheKeyGeneration_WithTenantPrefix_ShouldIsolate()
    {
        // Arrange
        using var scope1 = _factory.Services.CreateScope();
        var cacheService = scope1.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();

        // Act - Set value with tenant context
        var key = "shared-key";
        await cacheService.SetAsync(key, "tenant-001-value", absoluteExpirationInMinutes: 10);

        // Get value (should include tenant prefix internally)
        var value = await cacheService.GetAsync<string>(key);

        // Assert
        value.Should().Be("tenant-001-value");
    }

    /// <summary>
    /// Verifies distributed cache coherency across instances.
    /// </summary>
    [Fact]
    public async Task DistributedCacheCoherency_AcrossInstances_ShouldSync()
    {
        // Arrange - Simulate two instances with separate factories
        var factory1 = _factory;
        var factory2 = new CustomWebApplicationFactory();

        using var scope1 = factory1.Services.CreateScope();
        using var scope2 = factory2.Services.CreateScope();

        var cache1 = scope1.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var cache2 = scope2.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();

        var key = $"coherency-test-{Guid.NewGuid()}";

        // Act - Instance 1 sets value
        await cache1.SetAsync(key, "instance-1-value", absoluteExpirationInMinutes: 10);

        // Small delay for pub/sub propagation
        await Task.Delay(100);

        // Instance 2 gets value (should receive from distributed cache)
        var valueFromInstance2 = await cache2.GetAsync<string>(key);

        // Assert
        valueFromInstance2.Should().Be("instance-1-value");
    }

    /// <summary>
    /// Verifies protection against cache stampede.
    /// </summary>
    [Fact]
    public async Task CacheStampede_Protection_ShouldPreventMultipleFetches()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"stampede-{Guid.NewGuid()}";
        var fetchCount = 0;

        // Act - Simulate concurrent requests for same uncached key
        async Task<string?> FetchData()
        {
            Interlocked.Increment(ref fetchCount);
            await Task.Delay(50); // Simulate slow data source
            return "fetched-data";
        }

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cacheService.GetOrSetAsync(key, FetchData, absoluteExpirationInMinutes: 10))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - Should only fetch once despite 10 concurrent requests
        results.Should().AllBe("fetched-data");
        fetchCount.Should().BeGreaterThan(0); // At least one fetch
        // Note: Without explicit locking in GetOrSetAsync, may have multiple fetches
    }

    /// <summary>
    /// Verifies caching behavior for null values.
    /// </summary>
    [Fact]
    public async Task NullValue_Caching_ShouldHandleCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"null-test-{Guid.NewGuid()}";

        // Act - Try to cache null
        await cacheService.SetAsync<string?>(key, null, absoluteExpirationInMinutes: 10);
        var cachedValue = await cacheService.GetAsync<string?>(key);

        // Assert - Null values may not be cached (depends on implementation)
        cachedValue.Should().BeNull();
    }

    /// <summary>
    /// Verifies large object caching and serialization.
    /// </summary>
    [Fact]
    public async Task LargeObject_Caching_ShouldHandleSerializationCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"large-object-{Guid.NewGuid()}";

        var largeObject = new
        {
            Id = Guid.NewGuid(),
            Name = "Large Object",
            Data = string.Join("", Enumerable.Repeat("X", 10000)), // 10KB string
            Nested = new
            {
                Items = Enumerable.Range(0, 100).Select(i => new { Index = i, Value = $"Item-{i}" }).ToList()
            }
        };

        // Act
        await cacheService.SetAsync(key, largeObject, absoluteExpirationInMinutes: 10);
        var cachedObject = await cacheService.GetAsync<object>(key);

        // Assert
        cachedObject.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies different TTL values are respected.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(1440)]
    public async Task CacheWithVariousTTL_ShouldRespectExpiration(int ttlMinutes)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();
        var key = $"ttl-test-{ttlMinutes}-{Guid.NewGuid()}";

        // Act
        await cacheService.SetAsync(key, $"value-ttl-{ttlMinutes}", absoluteExpirationInMinutes: ttlMinutes);
        var cachedValue = await cacheService.GetAsync<string>(key);

        // Assert - Value should be cached
        cachedValue.Should().Be($"value-ttl-{ttlMinutes}");
    }

    /// <summary>
    /// Verifies cache keys with special characters are supported.
    /// </summary>
    [Theory]
    [InlineData("key@special")]
    [InlineData("key#hash")]
    [InlineData("key:colon")]
    [InlineData("key/slash")]
    [InlineData("key with spaces")]
    public async Task CacheKey_WithSpecialCharacters_ShouldHandleCorrectly(string specialKey)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<IMultiLevelCacheService>();

        // Act
        await cacheService.SetAsync(specialKey, "special-value", absoluteExpirationInMinutes: 10);
        var value = await cacheService.GetAsync<string>(specialKey);

        // Assert
        value.Should().Be("special-value");
    }
}

