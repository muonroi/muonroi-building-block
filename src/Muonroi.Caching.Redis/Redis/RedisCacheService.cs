using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Caching.Abstractions.Distributed;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;

namespace Muonroi.Caching.Redis.Redis;

/// <summary>
/// Implementation of <see cref="IMCacheService"/> using Redis and integrated with Muonroi ecosystem.
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache distributedCache,
    IMJsonSerializeService jsonSerializeService,
    IMDateTimeService dateTimeService,
    ITenantContext tenantContext,
    ILicenseGuard licenseGuard,
    IMLog<RedisCacheService> logger) : IMCacheService
{
    private const string FeatureKey = "distributed-cache";
    private const string Layer = "distributed";

    public async Task<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        EnsureLicensed();
        string cacheKey = BuildKey(key);
        string? tenantId = GetNormalizedTenantId();
        
        using var activity = StartActivity("get", cacheKey, tenantId);
        var sw = Stopwatch.StartNew();
        string status = "ok";
        bool hit = false;

        try
        {
            var data = await distributedCache.GetAsync(cacheKey, token);
            if (data is null) return default;

            hit = true;
            string valueString = Encoding.UTF8.GetString(data);
            return jsonSerializeService.Deserialize<T>(valueString);
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.Error(ex, "Failed to get cache for key {Key}", cacheKey);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("get", Layer, status, tenantId, hit, sw.Elapsed);
        }
    }

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken token = default)
    {
        EnsureLicensed();
        options ??= new CacheEntryOptions();
        string cacheKey = BuildKey(key, options);
        string? tenantId = GetNormalizedTenantId();

        using var activity = StartActivity("set", cacheKey, tenantId);
        var sw = Stopwatch.StartNew();
        string status = "ok";

        try
        {
            string serializeValue = jsonSerializeService.Serialize(value);
            byte[] data = Encoding.UTF8.GetBytes(serializeValue);

            var cacheOptions = new DistributedCacheEntryOptions();
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
            }
            if (options.SlidingExpiration.HasValue)
            {
                cacheOptions.SlidingExpiration = options.SlidingExpiration;
            }

            await distributedCache.SetAsync(cacheKey, data, cacheOptions, token);
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.Error(ex, "Failed to set cache for key {Key}", cacheKey);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("set", Layer, status, tenantId, false, sw.Elapsed);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        EnsureLicensed();
        string cacheKey = BuildKey(key);
        string? tenantId = GetNormalizedTenantId();

        using var activity = StartActivity("remove", cacheKey, tenantId);
        var sw = Stopwatch.StartNew();
        string status = "ok";

        try
        {
            await distributedCache.RemoveAsync(cacheKey, token);
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("remove", Layer, status, tenantId, false, sw.Elapsed);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        EnsureLicensed();
        string cacheKey = BuildKey(key);
        string? tenantId = GetNormalizedTenantId();

        using var activity = StartActivity("refresh", cacheKey, tenantId);
        var sw = Stopwatch.StartNew();
        string status = "ok";

        try
        {
            await distributedCache.RefreshAsync(cacheKey, token);
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("refresh", Layer, status, tenantId, false, sw.Elapsed);
        }
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken token = default) where T : class
    {
        EnsureLicensed();
        options ??= new CacheEntryOptions();
        string cacheKey = BuildKey(key, options);
        string? tenantId = GetNormalizedTenantId();

        using var activity = StartActivity("get_or_set", cacheKey, tenantId);
        var sw = Stopwatch.StartNew();
        string status = "ok";
        bool hit = false;

        try
        {
            var existing = await distributedCache.GetAsync(cacheKey, token);
            if (existing is { Length: > 0 })
            {
                string valueString = Encoding.UTF8.GetString(existing);
                var cachedData = jsonSerializeService.Deserialize<T>(valueString);
                if (cachedData is not null)
                {
                    hit = true;
                    return cachedData;
                }
            }

            var data = await factory();
            if (data is not null)
            {
                await SetAsync(key, data, options, token);
            }
            return data;
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            DistributedCacheRuntimeTelemetry.TrackOperation("get_or_set", Layer, status, tenantId, hit, sw.Elapsed);
        }
    }

    private void EnsureLicensed()
    {
        licenseGuard.EnsureFeature(FeatureKey);
    }

    private string BuildKey(string key, CacheEntryOptions? options = null)
    {
        string? tenantId = options?.TenantScoped == false ? null : tenantContext.TenantId;
        return DistributedCacheKeyBuilder.Build(key, options?.KeyNamespace, tenantId);
    }

    private string? GetNormalizedTenantId()
    {
        return DistributedCacheKeyBuilder.NormalizeTenantId(tenantContext.TenantId);
    }

    private static Activity? StartActivity(string operation, string cacheKey, string? tenantId)
    {
        var activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity($"distributed-cache.{operation}", ActivityKind.Internal);
        activity?.SetTag("cache.operation", operation);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);
        // We don't log raw keys for security/privacy, only hash if needed.
        return activity;
    }
}
