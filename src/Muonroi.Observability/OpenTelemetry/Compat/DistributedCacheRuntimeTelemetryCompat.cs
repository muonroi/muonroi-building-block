namespace Muonroi.Observability.OpenTelemetry.Compat;

/// <summary>
/// Compatibility wrapper for distributed cache telemetry.
/// </summary>
public static class DistributedCacheRuntimeTelemetry
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.ActivitySourceName;
    /// <summary>Meter name.</summary>
    public const string MeterName = Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.MeterName;

    /// <summary>Activity source instance.</summary>
    public static ActivitySource ActivitySource => Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.ActivitySource;

    /// <summary>Tracks a cache operation.</summary>
    /// <param name="operation">Operation name.</param>
    /// <param name="layer">Cache layer name.</param>
    /// <param name="status">Operation status.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="hit">Whether the cache hit occurred.</param>
    /// <param name="elapsed">Elapsed time.</param>
    public static void TrackOperation(
        string operation,
        string layer,
        string status,
        string? tenantId,
        bool hit,
        TimeSpan elapsed)
    {
        Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.TrackOperation(
            operation,
            layer,
            status,
            tenantId,
            hit,
            elapsed);
    }
}
