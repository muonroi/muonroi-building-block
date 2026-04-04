using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Default implementation of <see cref="ITelemetryDescriptor"/>.
/// </summary>
public class TelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames { get; init; } = [];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames { get; init; } = [];

    /// <summary>
    /// Creates a new descriptor with the specified activity sources and meters.
    /// </summary>
    /// <param name="activitySources">The activity source names.</param>
    /// <param name="meters">The meter names.</param>
    public static TelemetryDescriptor Create(IEnumerable<string> activitySources, IEnumerable<string> meters)
    {
        return new TelemetryDescriptor
        {
            ActivitySourceNames = activitySources,
            MeterNames = meters
        };
    }

    /// <summary>
    /// Creates a new descriptor with a single activity source and meter of the same name.
    /// </summary>
    /// <param name="name">The name for both activity source and meter.</param>
    public static TelemetryDescriptor Create(string name)
    {
        return new TelemetryDescriptor
        {
            ActivitySourceNames = [name],
            MeterNames = [name]
        };
    }
}
