using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Runtime.Telemetry;

/// <summary>
/// Telemetry descriptor for Rule Engine Runtime.
/// </summary>
public class RuntimeTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => ["Muonroi.Integration"];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => ["Muonroi.Integration"];
}
