using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Telemetry descriptor for distributed cache.
/// </summary>
public class DistributedCacheTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => [DistributedCacheRuntimeTelemetry.ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => [DistributedCacheRuntimeTelemetry.ActivitySourceName];
}
