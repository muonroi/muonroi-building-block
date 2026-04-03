using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Proliferation.Telemetry;

/// <summary>
/// Telemetry descriptor for Rule Proliferation Engine.
/// </summary>
public class ProliferationTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => ["Muonroi.RuleEngine.Proliferation"];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => ["Muonroi.RuleEngine.Proliferation"];
}
