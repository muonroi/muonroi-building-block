namespace Muonroi.Governance.License;

public static class AntiTamperingRuntimeTelemetry
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.AntiTampering";
    public const string MeterName = "Muonroi.BuildingBlock.AntiTampering";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> CheckCounter =
        Meter.CreateCounter<long>("antitamper_checks_total", description: "Total anti-tampering checks.");
    private static readonly Counter<long> DetectionCounter =
        Meter.CreateCounter<long>("antitamper_detections_total", description: "Total anti-tampering detections.");
    private static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>("antitamper_errors_total", description: "Total anti-tampering check errors.");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("antitamper_check_duration_ms", unit: "ms",
            description: "Anti-tampering check latency.");

    public static void TrackCheck(
        string scope,
        string status,
        string? tenantId,
        bool tamperDetected,
        TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { "antitamper.scope", scope },
            { "antitamper.status", status },
            { "antitamper.detected", tamperDetected },
            { "tenant.id", tenantId ?? string.Empty }
        };

        CheckCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (tamperDetected)
        {
            DetectionCounter.Add(1, tags);
        }

        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            ErrorCounter.Add(1, tags);
        }
    }
}
