namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Kafka Configs.
/// </summary>
public class KafkaConfigs
{
    /// <summary>
    /// Gets or sets the Host.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Topic.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Group Id.
    /// </summary>
    public string GroupId { get; set; } = "consumer-group";

    /// <summary>
    /// Gets or sets the Client Id.
    /// </summary>
    public string ClientId { get; set; } = "muonroi-messagebus";

    /// <summary>
    /// Gets or sets the Enable Auto Commit.
    /// </summary>
    public bool EnableAutoCommit { get; set; } = true;

    /// <summary>
    /// Gets or sets the Security Protocol.
    /// </summary>
    public string SecurityProtocol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sasl Mechanism.
    /// </summary>
    public string SaslMechanism { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sasl Username.
    /// </summary>
    public string SaslUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sasl Password.
    /// </summary>
    public string SaslPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Dead Letter Topic.
    /// </summary>
    public string DeadLetterTopic { get; set; } = string.Empty;
}
