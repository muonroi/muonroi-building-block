namespace Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Telemetry helpers for distributed cache operations.
/// </summary>
public static class DistributedCacheRuntimeTelemetry
{
    /// <summary>
    /// Activity source name for distributed cache tracing.
    /// </summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.DistributedCache";

    /// <summary>
    /// Meter name for distributed cache metrics.
    /// </summary>
    public const string MeterName = "Muonroi.BuildingBlock.DistributedCache";

    /// <summary>
    /// Activity source for distributed cache operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _operationCounter =
        _meter.CreateCounter<long>("distributed_cache_operations_total",
            description: "Total distributed-cache operations.");
    private static readonly Counter<long> _errorCounter =
        _meter.CreateCounter<long>("distributed_cache_errors_total",
            description: "Total distributed-cache errors.");
    private static readonly Histogram<double> _durationHistogram =
        _meter.CreateHistogram<double>("distributed_cache_operation_duration_ms", unit: "ms",
            description: "Distributed-cache operation latency.");

    /// <summary>
    /// Records metrics for a distributed cache operation.
    /// </summary>
    /// <param name="operation">Operation name.</param>
    /// <param name="layer">Cache layer.</param>
    /// <param name="status">Operation status.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="hit">True if cache hit.</param>
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
            { "cache.hit", hit },
            { "tenant.id", tenantId ?? string.Empty }
        };

        _operationCounter.Add(1, tags);
        _durationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            _errorCounter.Add(1, tags);
        }
    }
}
