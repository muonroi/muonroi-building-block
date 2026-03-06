namespace Muonroi.Messaging.MassTransit.Messaging;

public class MessageBusConfigs
{
    public const string SectionName = "MessageBusConfigs";

    public BusType BusType { get; set; }

    public RabbitMqConfigs? RabbitMq { get; set; }

    public KafkaConfigs? Kafka { get; set; }

    public MessageBusRuntimeConfigs Runtime { get; set; } = new();
}

public class MessageBusRuntimeConfigs
{
    public int RetryCount { get; set; } = 3;

    public int RetryIntervalMs { get; set; } = 500;

    public int PrefetchCount { get; set; } = 32;

    public int ConcurrentMessageLimit { get; set; } = 16;

    public bool EnableInMemoryOutbox { get; set; } = true;

    public string EndpointPrefix { get; set; } = string.Empty;
}
