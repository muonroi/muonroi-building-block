namespace Muonroi.Caching.Memory.MultiLevel;

/// <summary>
/// Abstraction for a multi-level cache service.
/// </summary>
public interface IMultiLevelCacheService
{
    /// <summary>
    /// Gets a cached value or computes and stores it.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="factory">Factory to create the value when missing.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached or computed value.</returns>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, int? absoluteExpirationInMinutes = 1440,
        CancellationToken token = default);

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="token">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, int? absoluteExpirationInMinutes = 1440, CancellationToken token = default);

    /// <summary>
    /// Gets a cached value.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached value or default.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken token = default);

    /// <summary>
    /// Removes a cached value.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken token = default);
}

/// <summary>
/// Implements multi-level caching using memory and distributed cache.
/// </summary>
public class MultiLevelCacheService : IMultiLevelCacheService
{
    private const string cacheOperation = "cache.operation";
    private const string cacheKeyHash = "cache.key_hash";
    private const string cacheTenantId = "tenant.id";
    private const string layerDistributed = "distributed";
    private const string statusError = "error";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _stampedeLocks = new(StringComparer.Ordinal);
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly LicenseState _licenseState;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly bool _usesExternalDistributedCache;
    private readonly bool _enableStampedeProtection;
    private readonly string _cacheKeyNamespace;
    private readonly int _defaultAbsoluteExpirationInMinutes;
    private readonly int _ttlJitterPercent;
    private readonly ISystemExecutionContextAccessor? _executionContextAccessor;

    /// <summary>
    /// Creates a multi-level cache service.
    /// </summary>
    /// <param name="memoryCache">Memory cache instance.</param>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="cacheConfigs">Optional cache configuration.</param>
    /// <param name="scopeFactory">Optional service scope factory.</param>
    /// <param name="executionContextAccessor">Optional execution context accessor.</param>
    public MultiLevelCacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        LicenseState? licenseState = null,
        CacheConfigs? cacheConfigs = null,
        IServiceScopeFactory? scopeFactory = null,
        ISystemExecutionContextAccessor? executionContextAccessor = null)
    {
        CacheConfigs configs = cacheConfigs ?? new CacheConfigs();
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _licenseState = licenseState ?? LicenseState.CreateFree();
        _scopeFactory = scopeFactory;
        _usesExternalDistributedCache = !IsInMemoryDistributedCache(_distributedCache);
        _enableStampedeProtection = configs.EnableStampedeProtection;
        _cacheKeyNamespace = configs.KeyNamespace?.Trim() ?? string.Empty;
        _defaultAbsoluteExpirationInMinutes = configs.DefaultAbsoluteExpirationInMinutes > 0
            ? configs.DefaultAbsoluteExpirationInMinutes
            : 1440;
        _ttlJitterPercent = Math.Clamp(configs.TtlJitterPercent, 0, 50);
        _executionContextAccessor = executionContextAccessor;
    }

    /// <summary>
    /// Gets a cached value or computes and stores it.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="factory">Factory to create the value when missing.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached or computed value.</returns>
    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, int? absoluteExpirationInMinutes = 1440,
        CancellationToken token = default)
    {
        EnsureDistributedCacheLicensed();
        string cacheKey = BuildCacheKey(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(ResolveTenantId());
        string layer = "none";
        bool hit = false;
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.get_or_set",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "get_or_set");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(MultiLevelCacheService.cacheTenantId, tenantId ?? string.Empty);

        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out T? memoryValue))
            {
                layer = "memory";
                hit = true;
                return memoryValue;
            }

            (bool Found, T? Value) distributedRead = await TryReadDistributedAsync<T>(cacheKey, token);
            if (distributedRead.Found)
            {
                layer = layerDistributed;
                hit = true;
                SetMemoryCache(cacheKey, distributedRead.Value, absoluteExpirationInMinutes);
                return distributedRead.Value;
            }

            SemaphoreSlim? stampedeLock = null;
            if (_enableStampedeProtection)
            {
                stampedeLock = _stampedeLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
                await stampedeLock.WaitAsync(token);
            }

            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out memoryValue))
                {
                    layer = "memory";
                    hit = true;
                    return memoryValue;
                }

                distributedRead = await TryReadDistributedAsync<T>(cacheKey, token);
                if (distributedRead.Found)
                {
                    layer = layerDistributed;
                    hit = true;
                    SetMemoryCache(cacheKey, distributedRead.Value, absoluteExpirationInMinutes);
                    return distributedRead.Value;
                }

                T? data = await factory();
                layer = "factory";
                if (data is null)
                {
                    return default;
                }

                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data); // MBB002-exempt: byte-level operation not in wrapper
                DistributedCacheEntryOptions options = CreateEntryOptions(absoluteExpirationInMinutes);
                SetMemoryCache(cacheKey, data, absoluteExpirationInMinutes);
                await _distributedCache.SetAsync(cacheKey, payload, options, token);
                return data;
            }
            finally
            {
                if (stampedeLock is not null)
                {
                    stampedeLock.Release();
                    if (stampedeLock.CurrentCount == 1)
                    {
                        _ = _stampedeLocks.TryRemove(cacheKey, out _);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            status = statusError;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("get_or_set", layer, status, tenantId, hit, sw.Elapsed);
        }
    }

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="token">Cancellation token.</param>
    public async Task SetAsync<T>(string key, T value, int? absoluteExpirationInMinutes = 1440,
        CancellationToken token = default)
    {
        EnsureDistributedCacheLicensed();
        string cacheKey = BuildCacheKey(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(ResolveTenantId());
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.set",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "set");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(MultiLevelCacheService.cacheTenantId, tenantId ?? string.Empty);

        try
        {
            DistributedCacheEntryOptions options = CreateEntryOptions(absoluteExpirationInMinutes);
            SetMemoryCache(cacheKey, value, absoluteExpirationInMinutes);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value); // MBB002-exempt: byte-level operation not in wrapper
            await _distributedCache.SetAsync(cacheKey, bytes, options, token);
        }
        catch (Exception ex)
        {
            status = statusError;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation(
                operation: "set",
                layer: layerDistributed,
                status: status,
                tenantId: tenantId,
                hit: false,
                elapsed: sw.Elapsed);
        }
    }

    /// <summary>
    /// Gets a cached value.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached value or default.</returns>
    public async Task<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        EnsureDistributedCacheLicensed();
        string cacheKey = BuildCacheKey(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(ResolveTenantId());
        string layer = "none";
        bool hit = false;
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.get",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "get");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(MultiLevelCacheService.cacheTenantId, tenantId ?? string.Empty);

        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out T? memoryValue))
            {
                layer = "memory";
                hit = true;
                return memoryValue;
            }

            (bool Found, T? Value) = await TryReadDistributedAsync<T>(cacheKey, token);
            if (Found)
            {
                layer = layerDistributed;
                hit = true;
                SetMemoryCache(cacheKey, Value, absoluteExpirationInMinutes: null);
                return Value;
            }

            layer = layerDistributed;
            return default;
        }
        catch (Exception ex)
        {
            status = statusError;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("get", layer, status, tenantId, hit, sw.Elapsed);
        }
    }

    /// <summary>
    /// Removes a cached value.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="token">Cancellation token.</param>
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        EnsureDistributedCacheLicensed();
        string cacheKey = BuildCacheKey(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(ResolveTenantId());
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.remove",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "remove");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(MultiLevelCacheService.cacheTenantId, tenantId ?? string.Empty);

        try
        {
            _memoryCache.Remove(cacheKey);
            await _distributedCache.RemoveAsync(cacheKey, token);
        }
        catch (Exception ex)
        {
            status = statusError;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation(
                operation: "remove",
                layer: layerDistributed,
                status: status,
                tenantId: tenantId,
                hit: false,
                elapsed: sw.Elapsed);
        }
    }

    private string BuildCacheKey(string key)
    {
        return DistributedCacheKeyBuilder.Build(key, _cacheKeyNamespace, ResolveTenantId());
    }

    private string? ResolveTenantId()
    {
        string? contextTenantId = _executionContextAccessor?.Get().TenantId;
        return string.IsNullOrWhiteSpace(contextTenantId) ? TenantContext.CurrentTenantId : contextTenantId;
    }

    private static bool IsInMemoryDistributedCache(IDistributedCache distributedCache)
    {
        string name = distributedCache.GetType().Name;
        return name.Contains("MemoryDistributedCache", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("InMemoryDistributedCache", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool Found, T? Value) TryDeserialize<T>(byte[] cacheValue)
    {
        if (cacheValue.Length == 0)
        {
            return (false, default);
        }

        string valueString = Encoding.UTF8.GetString(cacheValue);
        if (string.IsNullOrWhiteSpace(valueString))
        {
            return (false, default);
        }

        T? cachedData = JsonSerializer.Deserialize<T>(valueString); // MBB002-exempt: static helper method — raw deserialization from distributed cache bytes
        if (cachedData is null)
        {
            return (false, default);
        }

        return (true, cachedData);
    }

    private async Task<(bool Found, T? Value)> TryReadDistributedAsync<T>(string key, CancellationToken token)
    {
        byte[]? cacheValue = await _distributedCache.GetAsync(key, token);
        if (cacheValue is not { Length: > 0 })
        {
            return (false, default);
        }

        return TryDeserialize<T>(cacheValue);
    }

    private void SetMemoryCache<T>(string cacheKey, T value, int? absoluteExpirationInMinutes)
    {
        _ = _memoryCache.Set(cacheKey, value, ResolveExpiration(absoluteExpirationInMinutes));
    }

    private DistributedCacheEntryOptions CreateEntryOptions(int? absoluteExpirationInMinutes)
    {
        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = ResolveExpiration(absoluteExpirationInMinutes)
        };
        return options;
    }

    private TimeSpan ResolveExpiration(int? absoluteExpirationInMinutes)
    {
        int ttlInMinutes = absoluteExpirationInMinutes is > 0
            ? absoluteExpirationInMinutes.Value
            : _defaultAbsoluteExpirationInMinutes;

        if (_ttlJitterPercent > 0)
        {
            double jitterRange = ttlInMinutes * _ttlJitterPercent / 100.0;
            double offset = (Random.Shared.NextDouble() * 2 * jitterRange) - jitterRange;
            ttlInMinutes = Math.Max(1, (int)Math.Round(ttlInMinutes + offset));
        }

        return TimeSpan.FromMinutes(ttlInMinutes);
    }

    private static string ComputeKeyHash(string cacheKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    private void EnsureDistributedCacheLicensed()
    {
        // Memory-only fallback remains available in Free mode.
        if (!_usesExternalDistributedCache)
        {
            return;
        }

        if (_scopeFactory is not null)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILicenseGuard? licenseGuard = scope.ServiceProvider.GetService<ILicenseGuard>();
            if (licenseGuard is not null)
            {
                licenseGuard.EnsureFeature(FreeTierFeatures.Premium.DistributedCache);
                return;
            }
        }

        if (!_licenseState.HasFeature(FreeTierFeatures.Premium.DistributedCache))
        {
            throw new InvalidOperationException(
                "[LICENSE] Feature 'distributed-cache' is not available under your current license.");
        }
    }
}
