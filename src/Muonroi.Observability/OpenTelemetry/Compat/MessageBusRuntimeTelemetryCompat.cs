using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Observability.OpenTelemetry.Compat;

public static class MessageBusRuntimeTelemetry
{
    public const string ActivitySourceName = Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.ActivitySourceName;
    public const string MeterName = Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.MeterName;

    public static ActivitySource ActivitySource => Muonroi.Messaging.Abstractions.Events.MessageBusRuntimeTelemetry.ActivitySource;

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
