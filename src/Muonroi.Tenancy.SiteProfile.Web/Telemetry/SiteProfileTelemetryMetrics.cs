using System.Diagnostics.Metrics;

namespace Muonroi.Tenancy.SiteProfile.Web.Telemetry;

/// <summary>
/// Holds the OTel Meter and instruments for site-scoped telemetry.
/// Singleton — Meter instances must not be created per-request.
/// </summary>
public static class SiteProfileTelemetryMetrics
{
    /// <summary>OTel Meter name: "Muonroi.Tenancy.SiteProfile".</summary>
    public const string MeterName = "Muonroi.Tenancy.SiteProfile";

    private static readonly Meter _meter = new(MeterName, "1.0");

    /// <summary>
    /// Counter: site_profile_requests_total.
    /// Dimensions: site_id (string).
    /// Incremented once per request processed through UseSiteProfileTelemetry().
    /// </summary>
    public static readonly Counter<long> RequestCounter = _meter.CreateCounter<long>(
        "site_profile_requests_total",
        unit: "{request}",
        description: "Total number of HTTP requests processed per site profile.");
}
