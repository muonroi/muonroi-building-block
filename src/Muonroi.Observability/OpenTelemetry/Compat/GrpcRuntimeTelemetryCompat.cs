namespace Muonroi.Observability.OpenTelemetry.Compat;

/// <summary>
/// Lightweight gRPC telemetry helpers for OpenTelemetry.
/// </summary>
public static class GrpcRuntimeTelemetry
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Grpc";
    /// <summary>Meter name.</summary>
    public const string MeterName = "Muonroi.BuildingBlock.Grpc";

    /// <summary>Activity source instance.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>("grpc_requests_total", description: "Total gRPC requests");
    private static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>("grpc_errors_total", description: "Total gRPC errors");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("grpc_request_duration_ms", unit: "ms", description: "gRPC request duration");

    /// <summary>Tracks a gRPC request.</summary>
    /// <param name="method">gRPC method name.</param>
    /// <param name="callType">Call type (unary, streaming, etc.).</param>
    /// <param name="statusCode">gRPC status code.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="elapsed">Elapsed time.</param>
    public static void TrackRequest(
        string method,
        string callType,
        string statusCode,
        string? tenantId,
        TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { "grpc.method", method },
            { "grpc.call_type", callType },
            { "grpc.status_code", statusCode },
            { "tenant.id", tenantId ?? string.Empty }
        };

        RequestCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (!string.Equals(statusCode, "OK", StringComparison.OrdinalIgnoreCase))
        {
            ErrorCounter.Add(1, tags);
        }
    }
}
