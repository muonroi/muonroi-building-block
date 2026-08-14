namespace Muonroi.AspNetCore.Telemetry;

/// <summary>
/// Telemetry descriptor for UI engine changes.
/// </summary>
public class UiEngineTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => ["Muonroi.UiEngine.Changes"];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => ["Muonroi.UiEngine.Changes"];
}
