using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Observability.OpenTelemetry.Compat;

/// <summary>
/// Compatibility wrapper for message bus telemetry.
/// </summary>
public static class MessageBusRuntimeTelemetry
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.ActivitySourceName;
    /// <summary>Meter name.</summary>
    public const string MeterName = Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.MeterName;

    /// <summary>Activity source instance.</summary>
    public static ActivitySource ActivitySource => Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.ActivitySource;

    /// <summary>Tracks a message bus operation.</summary>
    /// <param name="operation">Operation name.</param>
    /// <param name="messageType">Message type.</param>
    /// <param name="destination">Destination address.</param>
    /// <param name="transport">Transport name.</param>
    /// <param name="status">Operation status.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="elapsed">Elapsed time.</param>
    public static void TrackOperation(
        string operation,
        string messageType,
        string destination,
        string transport,
        string status,
        string? tenantId,
        TimeSpan elapsed)
    {
        Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.TrackOperation(operation, messageType, destination, transport, status, tenantId, elapsed);
    }
}
