namespace Muonroi.Observability.OpenTelemetry;

/// <summary>
/// Centralized meter management for Muonroi-specific metrics.
/// </summary>
public static class MuonroiMetrics
{
    private const string MeterName = "Muonroi.Ecosystem.Core";
    /// <summary>
    /// The central meter instance for all Muonroi metrics, ensuring consistent naming and configuration across the ecosystem.
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Counts guard violations by type.
    /// </summary>
    public static readonly Counter<long> GuardViolations = Meter.CreateCounter<long>(
        "muonroi.guard.violations",
        unit: "{violation}",
        description: "Counts total guard violations by type.");

    /// <summary>
    /// Counts exceptions by category and error code.
    /// </summary>
    public static readonly Counter<long> ExceptionCount = Meter.CreateCounter<long>(
        "muonroi.exception.total",
        unit: "{exception}",
        description: "Counts total exceptions by category and error code.");

    /// <summary>
    /// Counts retry attempts by service.
    /// </summary>
    public static readonly Counter<long> RetryAttemptCount = Meter.CreateCounter<long>(
        "muonroi.retry.attempts",
        unit: "{retry}",
        description: "Counts total retry attempts by service.");
}
