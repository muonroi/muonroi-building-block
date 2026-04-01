using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.CEP.Observability;

/// <summary>
/// Telemetry descriptor for CEP.
/// </summary>
public class CepTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => [CepMetrics.ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => [CepMetrics.MeterName];
}
