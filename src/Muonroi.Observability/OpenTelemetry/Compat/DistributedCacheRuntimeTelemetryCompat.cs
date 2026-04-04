using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Muonroi.Observability.OpenTelemetry.Compat;

/// <summary>
/// Self-contained distributed cache telemetry for OpenTelemetry.
/// Inlines constants previously delegated to Muonroi.Caching.Abstractions.
/// </summary>
public static class DistributedCacheRuntimeTelemetry
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.DistributedCache";
    /// <summary>Meter name.</summary>
    public const string MeterName = "Muonroi.BuildingBlock.DistributedCache";

    /// <summary>Activity source instance.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("distributed_cache_operations_total", description: "Total distributed cache operations");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("distributed_cache_operation_duration_ms", unit: "ms", description: "Cache operation duration");

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
        TagList tags = new()
        {
            { "cache.operation", operation },
            { "cache.layer", layer },
            { "cache.status", status },
            { "tenant.id", tenantId ?? string.Empty },
            { "cache.hit", hit.ToString() }
        };
        OperationCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);
    }
}
