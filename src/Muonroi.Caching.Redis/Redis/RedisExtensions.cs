using Muonroi.Core.Abstractions.Configuration;

namespace Muonroi.Caching.Redis.Redis;

public static class RedisExtensions
{
    private const string cacheOperation = "cache.operation";
    private const string cacheKeyHash = "cache.key_hash";
    private const string cacheTenantId = "tenant.id";
    private const string statusError = "error";
    private const string layerDistributed = "distributed";

    public static IServiceCollection AddDapperCaching(this IServiceCollection services, IConfiguration configuration,
        RedisConfigs redisConfigs)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.DistributedCache);

        if (!redisConfigs.Enable)
        {
            return services;
        }

        ConnectionStringBuilder s = new()
        {
            Host = $"{redisConfigs.Host}:{redisConfigs.Port}",
            Password = redisConfigs.Password
        };
        RedisClient redisClient = new(s);

        RedisConfiguration config = new()
        {
            AllMethodsEnableCache = redisConfigs.AllMethodsEnableCache,
            Expire = TimeSpan.FromMinutes(redisConfigs.Expire),
            KeyPrefix = redisConfigs.KeyPrefix
        };
        services.AddDapperCachingInRedis(config, redisClient);

        services.AddSingleton<Dapper.Extensions.Caching.ICacheProvider, RedisCacheProvider>();

        return services;
    }

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration,
        RedisConfigs redisConfigs)
    {
        ArgumentNullException.ThrowIfNull(configuration);
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
        if (string.IsNullOrEmpty(redisConfigs.Host) || string.IsNullOrEmpty(redisConfigs.Port))
        {
            throw new InvalidOperationException(
                $"Invalid {RedisConfigs.DefaultSectionName}: Host and Port are required");
        }

        services.AddStackExchangeRedisCache(option =>
        {
            option.InstanceName = redisConfigs.KeyPrefix;
            ConfigurationOptions configOptions = new()
            {
                EndPoints = { { redisConfigs.Host, int.Parse(redisConfigs.Port) } },
                AllowAdmin = redisConfigs.AllowAdmin,
                AbortOnConnectFail = redisConfigs.AbortOnConnectFail
            };
            // Only set password if provided
            if (!string.IsNullOrEmpty(redisConfigs.Password))
            {
                configOptions.Password = redisConfigs.Password;
            }

            option.ConfigurationOptions = configOptions;
        });

        // Build connection string - only include password if provided
        string connectionString = string.IsNullOrEmpty(redisConfigs.Password)
            ? $"{redisConfigs.Host}:{redisConfigs.Port}"
            : $"{redisConfigs.Host}:{redisConfigs.Port},password={redisConfigs.Password}";
        services.AddSingleton(_ => new RedisClient(connectionString));

        return services;
    }

    public static async Task<string?> GetCacheAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
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

    public static async Task<T?> GetCacheAsync<T>(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
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

    public static async Task SetCacheAsync<T>(this IDistributedCache distributedCache, string key, T value,
        int? absoluteExpirationInMinutes = 1440,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
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

    public static async Task RemoveAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
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

    public static async Task RefreshAsync(this IDistributedCache distributedCache, string key,
        LicenseState? licenseState = null,
        ILicenseGuard? licenseGuard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
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
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(cacheData);
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

        if ((licenseState ?? LicenseState.CreateFree()).HasFeature(FreeTierFeatures.Premium.DistributedCache))
        {
            return;
        }

        throw new InvalidOperationException(
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
