using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Abstractions.Telemetry;

/// <summary>
/// Telemetry descriptor for Muonroi.RuleEngine.
/// </summary>
public class RuleEngineTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => ["Muonroi.RuleEngine"];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => ["Muonroi.RuleEngine"];
}
