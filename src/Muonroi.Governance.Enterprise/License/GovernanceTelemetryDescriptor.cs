namespace Muonroi.Governance.Enterprise.License;

/// <summary>
/// Telemetry descriptor for governance (anti-tampering and audit trail).
/// </summary>
public class GovernanceTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => 
    [
        AntiTamperingRuntimeTelemetry.ActivitySourceName,
        AuditTrailRuntimeTelemetry.ActivitySourceName
    ];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => 
    [
        AntiTamperingRuntimeTelemetry.ActivitySourceName,
        AuditTrailRuntimeTelemetry.ActivitySourceName
    ];
}
