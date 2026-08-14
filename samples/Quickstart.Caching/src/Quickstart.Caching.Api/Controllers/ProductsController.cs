namespace Quickstart.Caching.Api.Controllers;

/// <summary>
/// Demonstrates every operation exposed by IMultiLevelCacheService and the
/// DistributedCacheKeyBuilder helper.
///
/// The in-memory "database" is a static dictionary so the values survive across
/// requests within one process run.  In a real application this would be a
/// repository or EF Core DbContext.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController(IMultiLevelCacheService cache) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // Fake backing store — simulates a slow database
    // ---------------------------------------------------------------------------
    private static readonly Dictionary<int, ProductDto> _store = new()
    {
        [1] = new ProductDto(1, "Widget Alpha",   9.99m,  DateTime.UtcNow),
        [2] = new ProductDto(2, "Gadget Beta",   24.50m,  DateTime.UtcNow),
        [3] = new ProductDto(3, "Doohickey Gamma", 4.00m, DateTime.UtcNow),
    };

    // ---------------------------------------------------------------------------
    // 1. Cache-aside pattern — most common usage
    //    GET /api/products/{id}
    //
    //    GetOrSetAsync checks memory first, then the distributed cache.
    //    On a miss it calls the factory, stores the result in both layers, and
    //    returns it.  Subsequent calls for the same id return from cache without
    //    hitting the "database".
    //
    //    absoluteExpirationInMinutes drives both layers.  Omit it to use the
    //    value from CacheConfigs.DefaultAbsoluteExpirationInMinutes.
    // ---------------------------------------------------------------------------
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(int id, CancellationToken token)
    {
        // Cache key convention: simple string, scoped by namespace in CacheConfigs.
        string key = $"product:{id}";

        ProductDto? product = await cache.GetOrSetAsync<ProductDto>(
            key,
            factory: async () =>
            {
                // Simulate a slow database read — only executed on a cache miss.
                await Task.Delay(50, token);

                return _store.TryGetValue(id, out ProductDto? found)
                    ? found with { CachedAt = DateTime.UtcNow }
                    : null;
            },
            // Cache for 5 minutes.  Pass null to use the configured default.
            absoluteExpirationInMinutes: 5,
            token: token);

        if (product is null)
        {
            return NotFound(new { message = $"Product {id} not found." });
        }

        return Ok(product);
    }

    // ---------------------------------------------------------------------------
    // 2. Explicit set — useful when you want to pre-populate or refresh the cache
    //    without waiting for the next read.
    //    POST /api/products/{id}/cache
    //
    //    SetAsync writes to memory and the distributed cache simultaneously.
    //    Use this when you know the value has changed and want to push the update
    //    proactively (write-through pattern).
    // ---------------------------------------------------------------------------
    [HttpPost("{id:int}/cache")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutProductInCache(int id, CancellationToken token)
    {
        if (!_store.TryGetValue(id, out ProductDto? product))
        {
            return NotFound(new { message = $"Product {id} not found in backing store." });
        }

        // Stamp CachedAt so callers can see the value was freshly written.
        ProductDto updated = product with { CachedAt = DateTime.UtcNow };

        // Store explicitly for 10 minutes.
        await cache.SetAsync(
            key: $"product:{id}",
            value: updated,
            absoluteExpirationInMinutes: 10,
            token: token);

        return Ok(new
        {
            message   = "Product written to cache.",
            product   = updated,
            expiresIn = "10 minutes"
        });
    }

    // ---------------------------------------------------------------------------
    // 3. Direct read — does NOT populate the cache on a miss
    //    GET /api/products/{id}/cache/direct
    //
    //    GetAsync checks memory then the distributed cache and returns null when
    //    the key is absent.  Use this when you only want to serve stale data from
    //    cache and have a separate background job that refreshes it.
    // ---------------------------------------------------------------------------
    [HttpGet("{id:int}/cache/direct")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetProductFromCacheOnly(int id, CancellationToken token)
    {
        ProductDto? cached = await cache.GetAsync<ProductDto>($"product:{id}", token);

        if (cached is null)
        {
            // Return 204 so callers know the key does not exist rather than treating
            // it as a server error.
            return NoContent();
        }

        return Ok(new { source = "cache", product = cached });
    }

    // ---------------------------------------------------------------------------
    // 4. Cache eviction
    //    DELETE /api/products/{id}/cache
    //
    //    RemoveAsync evicts the entry from both memory and the distributed cache.
    //    Use this after a write to the backing store so the next read triggers a
    //    fresh factory call (cache-invalidation pattern).
    // ---------------------------------------------------------------------------
    [HttpDelete("{id:int}/cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EvictProductFromCache(int id, CancellationToken token)
    {
        await cache.RemoveAsync($"product:{id}", token);

        return Ok(new { message = $"Cache entry 'product:{id}' evicted from all layers." });
    }

    // ---------------------------------------------------------------------------
    // 5. Cache warming — pre-populate multiple keys in one go
    //    POST /api/products/cache/warm
    //
    //    Calls SetAsync for every product in the store.  Useful at application
    //    startup or after a bulk data import.
    // ---------------------------------------------------------------------------
    [HttpPost("cache/warm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> WarmCache(CancellationToken token)
    {
        List<string> warmed = [];

        foreach ((int id, ProductDto product) in _store)
        {
            string key = $"product:{id}";
            await cache.SetAsync(key, product with { CachedAt = DateTime.UtcNow },
                absoluteExpirationInMinutes: 5, token: token);
            warmed.Add(key);
        }

        return Ok(new { message = "Cache warmed.", keys = warmed });
    }

    // ---------------------------------------------------------------------------
    // 6. DistributedCacheKeyBuilder — key composition helper
    //    GET /api/products/cache-key
    //
    //    Shows how to build cache keys manually with namespace and optional
    //    tenant isolation.  The multi-level service uses this internally but
    //    you can call it directly to log or display the resolved key.
    // ---------------------------------------------------------------------------
    [HttpGet("cache-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ShowCacheKeyComposition(
        [FromQuery] string baseKey    = "product:42",
        [FromQuery] string? keyNamespace = "quickstart",
        [FromQuery] string? tenantId  = "tenant-abc")
    {
        // Normalize the raw tenant id (trims whitespace, returns null when blank).
        string? normalizedTenant = DistributedCacheKeyBuilder.NormalizeTenantId(tenantId);

        // Build the fully-qualified cache key used internally by the services.
        string fullyQualifiedKey = DistributedCacheKeyBuilder.Build(baseKey, keyNamespace, tenantId);

        // Demonstrate all three composition modes side by side.
        string keyNoNamespaceNoTenant  = DistributedCacheKeyBuilder.Build(baseKey);
        string keyNamespaceOnly        = DistributedCacheKeyBuilder.Build(baseKey, keyNamespace);
        string keyTenantOnly           = DistributedCacheKeyBuilder.Build(baseKey, tenantId: tenantId);
        string keyNamespaceAndTenant   = DistributedCacheKeyBuilder.Build(baseKey, keyNamespace, tenantId);

        return Ok(new
        {
            inputs = new
            {
                baseKey,
                keyNamespace,
                rawTenantId    = tenantId,
                normalizedTenant
            },
            results = new
            {
                // "product:42"
                noNamespaceNoTenant  = keyNoNamespaceNoTenant,
                // "quickstart:product:42"
                namespaceOnly        = keyNamespaceOnly,
                // "tenant-abc:product:42"
                tenantOnly           = keyTenantOnly,
                // "quickstart:tenant-abc:product:42"
                namespaceAndTenant   = keyNamespaceAndTenant,
                // Same as namespaceAndTenant when all params are provided
                fullyQualifiedKey
            },
            notes = new[]
            {
                "The multi-level cache service calls DistributedCacheKeyBuilder.Build() internally.",
                "KeyNamespace comes from CacheConfigs.KeyNamespace in appsettings.json.",
                "TenantId is resolved from the execution context (ISystemExecutionContextAccessor) or TenantContext.",
                "NormalizeTenantId() trims whitespace and returns null for blank/null input."
            }
        });
    }

    // ---------------------------------------------------------------------------
    // 7. CacheEntryOptions reference — demonstrates all IMCacheService options
    //    GET /api/products/cache-entry-options
    //
    //    IMCacheService.SetAsync / GetOrSetAsync accept CacheEntryOptions which
    //    exposes AbsoluteExpirationRelativeToNow, SlidingExpiration, KeyNamespace
    //    and TenantScoped.  This endpoint shows the available fields without
    //    requiring a Redis connection.
    // ---------------------------------------------------------------------------
    [HttpGet("cache-entry-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ShowCacheEntryOptions()
    {
        // CacheEntryOptions is the options record accepted by IMCacheService
        // (the distributed-only interface, implemented by RedisCacheService).
        // IMultiLevelCacheService uses plain int minutes instead, but the same
        // concepts apply.
        var absoluteOnly = new Muonroi.Caching.Abstractions.Distributed.CacheEntryOptions
        {
            // Expires 30 minutes from now regardless of access frequency.
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };

        var slidingOnly = new Muonroi.Caching.Abstractions.Distributed.CacheEntryOptions
        {
            // Resets the TTL on every access; entry lives as long as it is used.
            SlidingExpiration = TimeSpan.FromMinutes(10),
            // No absolute cap — disable default 24-hour absolute expiry.
            AbsoluteExpirationRelativeToNow = null
        };

        var combinedExpiry = new Muonroi.Caching.Abstractions.Distributed.CacheEntryOptions
        {
            // Sliding window of 5 min but hard cap of 60 min total.
            SlidingExpiration               = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };

        var namespacedAndTenantScoped = new Muonroi.Caching.Abstractions.Distributed.CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            // Prefix the key with a custom namespace regardless of CacheConfigs.
            KeyNamespace = "orders",
            // TenantScoped = true (default) — key includes the current tenant id.
            // Set to false for data shared across all tenants.
            TenantScoped = true
        };

        return Ok(new
        {
            description = "CacheEntryOptions fields accepted by IMCacheService (RedisCacheService). " +
                          "IMultiLevelCacheService uses absoluteExpirationInMinutes (int) instead.",
            examples = new
            {
                absoluteOnly = new
                {
                    absoluteOnly.AbsoluteExpirationRelativeToNow,
                    absoluteOnly.SlidingExpiration,
                    note = "Hard expiry — entry removed at T+30 min no matter how often it is read."
                },
                slidingOnly = new
                {
                    slidingOnly.AbsoluteExpirationRelativeToNow,
                    slidingOnly.SlidingExpiration,
                    note = "Entry stays alive indefinitely while accessed within 10-min windows."
                },
                combinedExpiry = new
                {
                    combinedExpiry.AbsoluteExpirationRelativeToNow,
                    combinedExpiry.SlidingExpiration,
                    note = "Sliding window cannot extend past the 60-min absolute cap."
                },
                namespacedAndTenantScoped = new
                {
                    namespacedAndTenantScoped.AbsoluteExpirationRelativeToNow,
                    namespacedAndTenantScoped.KeyNamespace,
                    namespacedAndTenantScoped.TenantScoped,
                    note = "Key will be: 'orders:{tenantId}:{baseKey}'. Use TenantScoped=false for shared data."
                }
            }
        });
    }
}
