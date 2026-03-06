using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Muonroi.Messaging.Abstractions.Events;

public static class MessageBusRuntimeTelemetry
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.MessageBus";
    public const string MeterName = "Muonroi.BuildingBlock.MessageBus";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> MessageCounter =
        Meter.CreateCounter<long>("messagebus_messages_total", description: "Total processed messages."); 
    private static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>("messagebus_errors_total", description: "Total failed message operations.");
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("messagebus_operation_duration_ms", unit: "ms",
            description: "Message bus operation latency.");

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
            { "messaging.system", transport },
            { "messaging.status", status },
            { "tenant.id", tenantId ?? string.Empty }
        };

        MessageCounter.Add(1, tags);
        DurationHistogram.Record(elapsed.TotalMilliseconds, tags);

        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            ErrorCounter.Add(1, tags);
        }
    }

    public static string ResolveTransport(Uri? destinationAddress)
    {
        if (destinationAddress is null)
        {
            return "unknown";
        }

        string scheme = destinationAddress.Scheme;
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return "unknown";
        }

        if (scheme.Contains("rabbit", StringComparison.OrdinalIgnoreCase))
        {
            return "rabbitmq";
        }

        if (scheme.Contains("kafka", StringComparison.OrdinalIgnoreCase))
        {
            return "kafka";
        }

        return scheme;
    }
}
