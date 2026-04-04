using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Telemetry descriptor for gRPC.
/// </summary>
public class GrpcTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => [GrpcRuntimeTelemetry.ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => [GrpcRuntimeTelemetry.ActivitySourceName];
}
