namespace Muonroi.Observability.OpenTelemetry.Compat;

public static class DistributedCacheRuntimeTelemetry
{
    public const string ActivitySourceName = Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.ActivitySourceName;
    public const string MeterName = Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.MeterName;

    public static ActivitySource ActivitySource => Caching.Abstractions.Distributed.DistributedCacheRuntimeTelemetry.ActivitySource;

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
