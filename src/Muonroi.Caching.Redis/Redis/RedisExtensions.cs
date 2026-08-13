using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Caching.Abstractions.Distributed;
using Muonroi.Caching.Redis.Routing;
using Muonroi.Core.Abstractions.Configuration;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Tenancy.Core;
using StackExchange.Redis;

namespace Muonroi.Caching.Redis.Redis;

/// <summary>
/// Redis registration and cache helper extensions.
/// </summary>
public static class RedisExtensions
{
    private const string cacheOperation = "cache.operation";
    private const string cacheKeyHash = "cache.key_hash";
    private const string cacheTenantId = "tenant.id";
    private const string statusError = "error";
    private const string layerDistributed = "distributed";

    /// <summary>
    /// Registers Redis distributed cache services and the integrated <see cref="IMCacheService"/>.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="configuration">Configuration source.</param>
    /// <param name="redisConfigs">Redis configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration,
        RedisConfigs redisConfigs)
    {
        MGuard.NotNull(configuration);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.DistributedCache);

        // Skip Redis setup if disabled
        if (!redisConfigs.Enable)
        {
            return services;
        }

        string? host = configuration[$"{RedisConfigs.DefaultSectionName}:Host"];
        string? port = configuration[$"{RedisConfigs.DefaultSectionName}:Port"];
        string? pwd = configuration[$"{RedisConfigs.DefaultSectionName}:Password"];
        string? prefix = configuration[$"{RedisConfigs.DefaultSectionName}:KeyPrefix"];

        if (!string.IsNullOrWhiteSpace(host))
        {
            redisConfigs.Host = host;
        }

        if (!string.IsNullOrWhiteSpace(port))
        {
            redisConfigs.Port = port;
        }

        if (!string.IsNullOrWhiteSpace(pwd))
        {
            redisConfigs.Password = pwd;
        }

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            redisConfigs.KeyPrefix = prefix;
        }

        // Only Host and Port are required - Password is optional for Redis instances without authentication
        MGuard.Configured(!string.IsNullOrEmpty(redisConfigs.Host) && !string.IsNullOrEmpty(redisConfigs.Port),
            $"Invalid {RedisConfigs.DefaultSectionName}: Host and Port are required", RedisConfigs.DefaultSectionName);

        ConfigurationOptions configurationOptions = new()
        {
            EndPoints = { { redisConfigs.Host, int.Parse(redisConfigs.Port) } },
            AllowAdmin = redisConfigs.AllowAdmin,
            AbortOnConnectFail = redisConfigs.AbortOnConnectFail
        };
        if (!string.IsNullOrEmpty(redisConfigs.Password))
        {
            configurationOptions.Password = redisConfigs.Password;
        }

        services.AddStackExchangeRedisCache(option =>
        {
            option.InstanceName = redisConfigs.KeyPrefix;
            option.ConfigurationOptions = configurationOptions;
        });
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions));
        
        // Register the ecosystem-integrated cache service
        services.TryAddSingleton<IMCacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Registers the Redis-backed routing table store used by Track 8 message routing.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Optional routing table configuration.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddRedisRoutingTable(
        this IServiceCollection services,
        Action<RedisRoutingTableOptions>? configure = null)
    {
        MGuard.NotNull(services);

        services.AddOptions<RedisRoutingTableOptions>();
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<Muonroi.Messaging.Abstractions.Contracts.IDynamicRoutingTableStore, RedisRoutingTableStore>();
        return services;
    }

    /// <summary>
    /// Gets a cached string value from Redis.
    /// </summary>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached value or null.</returns>
    public static async Task<string?> GetCacheAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken token = default)
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);

        string cacheKey = DistributedCacheKeyBuilder.Build(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        bool hit = false;
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.get",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "get");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            byte[]? cacheValue = await distributedCache.GetAsync(cacheKey, token);
            hit = cacheValue is not null;
            return cacheValue is not null ? Encoding.UTF8.GetString(cacheValue) : default;
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
            DistributedCacheRuntimeTelemetry.TrackOperation("get", layerDistributed, status, tenantId, hit, sw.Elapsed);
        }
    }

    /// <summary>
    /// Gets a cached value from Redis and deserializes it.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value or default.</returns>
    public static async Task<T?> GetCacheAsync<T>(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);

        string cacheKey = DistributedCacheKeyBuilder.Build(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        bool hit = false;
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.get",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "get");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            byte[]? cacheValue = await distributedCache.GetAsync(cacheKey, cancellationToken);
            if (cacheValue is null)
            {
                return default;
            }

            hit = true;
            string valueString = Encoding.UTF8.GetString(cacheValue);
            return JsonSerializer.Deserialize<T>(valueString); // MBB002-exempt: static-class boundary
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
            DistributedCacheRuntimeTelemetry.TrackOperation("get", layerDistributed, status, tenantId, hit, sw.Elapsed);
        }
    }

    /// <summary>
    /// Stores a value in Redis.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SetCacheAsync<T>(this IDistributedCache distributedCache, string key, T value,
        int? absoluteExpirationInMinutes = 1440,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);
        string cacheKey = DistributedCacheKeyBuilder.Build(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.set",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "set");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            string serializeValue = JsonSerializer.Serialize(value); // MBB002-exempt: static-class boundary
            byte[] saveValue = Encoding.UTF8.GetBytes(serializeValue);

            DistributedCacheEntryOptions cacheOptions = new();
            if (absoluteExpirationInMinutes.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(absoluteExpirationInMinutes.Value);
            }

            await distributedCache.SetAsync(cacheKey, saveValue, cacheOptions, cancellationToken);
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
    /// Removes a cached value from Redis.
    /// </summary>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RemoveAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);
        string cacheKey = DistributedCacheKeyBuilder.Build(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.remove",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "remove");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            await distributedCache.RemoveAsync(cacheKey, cancellationToken);
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

    /// <summary>
    /// Refreshes a cached value in Redis.
    /// </summary>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RefreshAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);
        string cacheKey = DistributedCacheKeyBuilder.Build(key);
        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.refresh",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "refresh");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            await distributedCache.RefreshAsync(cacheKey, cancellationToken);
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
                operation: "refresh",
                layer: layerDistributed,
                status: status,
                tenantId: tenantId,
                hit: false,
                elapsed: sw.Elapsed);
        }
    }

    /// <summary>
    /// Gets a cached value or computes and stores it in Redis.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="distributedCache">Distributed cache instance.</param>
    /// <param name="key">Cache key.</param>
    /// <param name="cacheData">Factory to create the value when missing.</param>
    /// <param name="absoluteExpirationInMinutes">Absolute expiration in minutes.</param>
    /// <param name="licenseState">Optional license state.</param>
    /// <param name="licenseGuard">Optional license guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or computed value.</returns>
    public static async Task<T?> GetOrSetAsync<T>(this IDistributedCache distributedCache
        , string key
        , Func<Task<T?>> cacheData
        , int? absoluteExpirationInMinutes = 1440
        , LicenseState? licenseState = null
        , ILicenseGuard? licenseGuard = null
        , CancellationToken cancellationToken = default
       )
        where T : class
    {
        MGuard.NotNull(distributedCache);
        MGuard.NotEmpty(key);
        MGuard.NotNull(cacheData);
        EnsureDistributedCacheLicensed(distributedCache, licenseState, licenseGuard);

        string? tenantId = DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId);
        string status = "ok";
        bool hit = false;
        Stopwatch sw = Stopwatch.StartNew();
        string cacheKey = DistributedCacheKeyBuilder.Build(key);

        using Activity? activity = DistributedCacheRuntimeTelemetry.ActivitySource.StartActivity(
            "distributed-cache.get_or_set",
            ActivityKind.Internal);
        activity?.SetTag(cacheOperation, "get_or_set");
        activity?.SetTag(cacheKeyHash, ComputeKeyHash(cacheKey));
        activity?.SetTag(cacheTenantId, tenantId ?? string.Empty);

        try
        {
            byte[]? cacheValue = await distributedCache.GetAsync(cacheKey, cancellationToken);
            if (cacheValue is { Length: > 0 })
            {
                string valueString = Encoding.UTF8.GetString(cacheValue);
                if (!string.IsNullOrWhiteSpace(valueString))
                {
                    T? cachedData = JsonSerializer.Deserialize<T>(valueString); // MBB002-exempt: static-class boundary
                    if (cachedData is not null)
                    {
                        hit = true;
                        return cachedData;
                    }
                }
            }

            T? data = await cacheData();
            if (data is null)
            {
                return null;
            }

            string serializeValue = JsonSerializer.Serialize(data); // MBB002-exempt: static-class boundary
            byte[] saveValue = Encoding.UTF8.GetBytes(serializeValue);

            DistributedCacheEntryOptions cacheOptions = new();
            if (absoluteExpirationInMinutes.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(absoluteExpirationInMinutes.Value);
            }

            await distributedCache.SetAsync(cacheKey, saveValue, cacheOptions, cancellationToken);
            return data;
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
            DistributedCacheRuntimeTelemetry.TrackOperation("get_or_set", layerDistributed, status, tenantId, hit,
                sw.Elapsed);
        }
    }

    private static void EnsureDistributedCacheLicensed(
        IDistributedCache distributedCache,
        LicenseState? licenseState,
        ILicenseGuard? licenseGuard)
    {
        if (IsInMemoryDistributedCache(distributedCache))
        {
            return;
        }

        if (licenseGuard is not null)
        {
            licenseGuard.EnsureFeature(FreeTierFeatures.Premium.DistributedCache);
            return;
        }

        MGuard.State((licenseState ?? LicenseState.CreateFree()).HasFeature(FreeTierFeatures.Premium.DistributedCache),
            "[LICENSE] Feature 'distributed-cache' is not available under your current license.");
    }

    private static bool IsInMemoryDistributedCache(IDistributedCache distributedCache)
    {
        string name = distributedCache.GetType().Name;
        return name.Contains("MemoryDistributedCache", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("InMemoryDistributedCache", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeKeyHash(string cacheKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }
}
