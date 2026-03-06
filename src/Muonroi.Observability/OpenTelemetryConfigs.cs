namespace Muonroi.Observability;

/// <summary>
/// Configuration options for OpenTelemetry integration.
/// </summary>
public class OpenTelemetryConfigs
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// Service name reported to the OpenTelemetry backend.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// OTLP endpoint for exporting traces and metrics.
    /// </summary>
    public string? OtlpEndpoint { get; set; }
}
