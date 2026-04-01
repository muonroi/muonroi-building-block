using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Messaging.Abstractions.Events;

/// <summary>
/// Telemetry descriptor for message bus.
/// </summary>
public class MessageBusTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => [MessageBusRuntimeTelemetry.ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => [MessageBusRuntimeTelemetry.ActivitySourceName];
}
