using System.Diagnostics.Metrics;

namespace Muonroi.Grpc.Grpc;

public static class GrpcRuntimeTelemetry
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Grpc";
    public const string MeterName = "Muonroi.BuildingBlock.Grpc";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _requestCounter =
        _meter.CreateCounter<long>("grpc_requests_total", description: "Total gRPC requests");
    private static readonly Counter<long> _errorCounter =
        _meter.CreateCounter<long>("grpc_errors_total", description: "Total gRPC errors");
    private static readonly Histogram<double> _durationHistogram =
        _meter.CreateHistogram<double>("grpc_request_duration_ms", unit: "ms", description: "gRPC request duration");

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
