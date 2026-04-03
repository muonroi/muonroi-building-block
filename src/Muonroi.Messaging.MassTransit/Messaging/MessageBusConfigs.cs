namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Message Bus Configs.
/// </summary>
public class MessageBusConfigs
{
    /// <summary>
    /// The Section Name.
    /// </summary>
    public const string SectionName = "MessageBusConfigs";

    /// <summary>
    /// Gets or sets the Bus Type.
    /// </summary>
    public BusType BusType { get; set; }

    /// <summary>
    /// Gets or sets the Rabbit Mq.
    /// </summary>
    public RabbitMqConfigs? RabbitMq { get; set; }

    /// <summary>
    /// Gets or sets the Kafka.
    /// </summary>
    public KafkaConfigs? Kafka { get; set; }

    /// <summary>
    /// Executes the Runtime operation.
    /// </summary>
    public MessageBusRuntimeConfigs Runtime { get; set; } = new();

    /// <summary>
    /// Executes the Outbox Relay operation.
    /// </summary>
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

    /// <summary>
    /// If true, resolves dynamic routing rules from the Redis routing table before DI routers.
    /// </summary>
    public bool EnableRedisRoutingTable { get; set; } = false;
}

/// <summary>
/// Represents the Message Bus Runtime Configs.
/// </summary>
public class MessageBusRuntimeConfigs
{
    /// <summary>
    /// Gets or sets the Retry Count.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the Retry Interval Ms.
    /// </summary>
    public int RetryIntervalMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the Prefetch Count.
    /// </summary>
    public int PrefetchCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the Concurrent Message Limit.
    /// </summary>
    public int ConcurrentMessageLimit { get; set; } = 16;

    /// <summary>
    /// Activates MassTransit's per-message in-memory deduplication outbox.
    /// Note: This is a completely separate mechanism from the persistent EventOutbox (EF Core based).
    /// </summary>
    public bool EnableInMemoryOutbox { get; set; } = true;

    /// <summary>
    /// Gets or sets the Endpoint Prefix.
    /// </summary>
    public string EndpointPrefix { get; set; } = string.Empty;
}

/// <summary>
/// Represents the Outbox Relay Configs.
/// </summary>
public class OutboxRelayConfigs
{
    /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Gets or sets the Polling Interval Ms.
    /// </summary>
    public int PollingIntervalMs { get; set; } = 5000;
    /// <summary>
    /// Gets or sets the Batch Size.
    /// </summary>
    public int BatchSize { get; set; } = 100;
    /// <summary>
    /// Gets or sets the Max Retry Failed Count.
    /// </summary>
    public int MaxRetryFailedCount { get; set; } = 5;
}
