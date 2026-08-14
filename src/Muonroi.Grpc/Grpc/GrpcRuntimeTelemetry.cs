namespace Muonroi.Grpc.Grpc;

/// <summary>
/// Telemetry helpers for gRPC runtime metrics and tracing.
/// </summary>
public static class GrpcRuntimeTelemetry
{
    /// <summary>
    /// Activity source name for gRPC spans.
    /// </summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Grpc";
    /// <summary>
    /// Meter name for gRPC metrics.
    /// </summary>
    public const string MeterName = "Muonroi.BuildingBlock.Grpc";

    /// <summary>
    /// Activity source used for gRPC client spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _requestCounter =
        _meter.CreateCounter<long>("grpc_requests_total", description: "Total gRPC requests");
    private static readonly Counter<long> _errorCounter =
        _meter.CreateCounter<long>("grpc_errors_total", description: "Total gRPC errors");
    private static readonly Histogram<double> _durationHistogram =
        _meter.CreateHistogram<double>("grpc_request_duration_ms", unit: "ms", description: "gRPC request duration");

    /// <summary>
    /// Records a gRPC request metric with tags.
    /// </summary>
    public static void TrackRequest(string method, string callType, string statusCode, string? tenantId, TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { "grpc.method", method },
            { "grpc.call_type", callType },
            { "grpc.status_code", statusCode },
            { "tenant.id", tenantId ?? string.Empty }
        };

        _requestCounter.Add(1, tags);
        _durationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (!string.Equals(statusCode, nameof(StatusCode.OK), StringComparison.OrdinalIgnoreCase))
        {
            _errorCounter.Add(1, tags);
        }
    }
}
