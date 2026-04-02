namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Defines a descriptor for telemetry sources (activities and meters) within a package.
/// </summary>
public interface ITelemetryDescriptor
{
    /// <summary>
    /// Gets the list of ActivitySource names to register.
    /// </summary>
    IEnumerable<string> ActivitySourceNames { get; }

    /// <summary>
    /// Gets the list of Meter names to register.
    /// </summary>
    IEnumerable<string> MeterNames { get; }
}
