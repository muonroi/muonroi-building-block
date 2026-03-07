namespace Muonroi.Messaging.MassTransit.Messaging;

public class MessageBusConfigs
{
    public const string SectionName = "MessageBusConfigs";

    public BusType BusType { get; set; }

    public RabbitMqConfigs? RabbitMq { get; set; }

    public KafkaConfigs? Kafka { get; set; }

    public MessageBusRuntimeConfigs Runtime { get; set; } = new();

    public OutboxRelayConfigs OutboxRelay { get; set; } = new();

    /// <summary>
    /// If true, the raw access token is not sent in message headers.
    /// Instead, downstream services should re-authenticate or a masked signature is passed.
    /// </summary>
    public bool MaskAccessTokenInHeaders { get; set; } = false;

    /// <summary>
    /// If true, enforces tenant quota limits for messaging.
    /// </summary>
    public bool EnableQuotaEnforcement { get; set; } = false;

    /// <summary>
    /// If true, runs the Muonroi Rule Engine for each incoming message to decide routing/filtering.
    /// </summary>
    public bool EnableRuleEngineRouting { get; set; } = false;
}

public class MessageBusRuntimeConfigs
{
    public int RetryCount { get; set; } = 3;

    public int RetryIntervalMs { get; set; } = 500;

    public int PrefetchCount { get; set; } = 32;

    public int ConcurrentMessageLimit { get; set; } = 16;

    /// <summary>
    /// Activates MassTransit's per-message in-memory deduplication outbox.
    /// Note: This is a completely separate mechanism from the persistent EventOutbox (EF Core based).
    /// </summary>
    public bool EnableInMemoryOutbox { get; set; } = true;

    public string EndpointPrefix { get; set; } = string.Empty;
}

public class OutboxRelayConfigs
{
    public bool Enabled { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 5000;
    public int BatchSize { get; set; } = 100;
    public int MaxRetryFailedCount { get; set; } = 5;
}
