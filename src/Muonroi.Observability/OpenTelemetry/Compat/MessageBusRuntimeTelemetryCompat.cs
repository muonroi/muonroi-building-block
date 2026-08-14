namespace Muonroi.Observability.OpenTelemetry.Compat;

/// <summary>
/// Self-contained message bus telemetry for OpenTelemetry.
/// Inlines constants previously delegated to Muonroi.Messaging.Abstractions.
/// </summary>
public static class MessageBusRuntimeTelemetry
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.MessageBus";
    /// <summary>Meter name.</summary>
    public const string MeterName = "Muonroi.BuildingBlock.MessageBus";

    /// <summary>Activity source instance.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("message_bus_operations_total", description: "Total message bus operations");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("message_bus_operation_duration_ms", unit: "ms", description: "Message bus operation duration");

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
        TagList tags = new()
        {
            { "messaging.operation", operation },
            { "messaging.message_type", messageType },
            { "messaging.destination", destination },
            { "messaging.transport", transport },
            { "messaging.status", status },
            { "tenant.id", tenantId ?? string.Empty }
        };
        OperationCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);
    }
}
