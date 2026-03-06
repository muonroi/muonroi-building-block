namespace Muonroi.Governance.License;

public static class AuditTrailRuntimeTelemetry
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.AuditTrail";
    public const string MeterName = "Muonroi.BuildingBlock.AuditTrail";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("audittrail_operations_total", description: "Total audit trail operations.");
    private static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>("audittrail_errors_total", description: "Total audit trail operation errors.");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("audittrail_operation_duration_ms", unit: "ms",
            description: "Audit trail operation latency.");
    private static readonly Histogram<long> EntryHistogram =
        Meter.CreateHistogram<long>("audittrail_entries_count", unit: "items",
            description: "Audit trail entries processed per operation.");

    public static void TrackOperation(
        string operation,
        string status,
        string? tenantId,
        string chainStorage,
        TimeSpan elapsed,
        long entryCount = 0)
    {
        TagList tags = new()
        {
            { "audittrail.operation", operation },
            { "audittrail.status", status },
            { "audittrail.storage", chainStorage },
            { "tenant.id", tenantId ?? string.Empty }
        };

        OperationCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (entryCount > 0)
        {
            EntryHistogram.Record(entryCount, tags);
        }

        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            ErrorCounter.Add(1, tags);
        }
    }
}
