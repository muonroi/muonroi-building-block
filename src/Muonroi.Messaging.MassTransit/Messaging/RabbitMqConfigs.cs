namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Rabbit Mq Configs.
/// </summary>
public class RabbitMqConfigs
{
    /// <summary>
    /// Gets or sets the Host.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Virtual Host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the Username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the Use Ssl.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets the Ssl Server Name.
    /// </summary>
    public string SslServerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Heartbeat Seconds.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the Publisher Confirmation.
    /// </summary>
    public bool PublisherConfirmation { get; set; } = true;

    /// <summary>
    /// Gets or sets the Dead Letter Exchange.
    /// </summary>
    public string DeadLetterExchange { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Use Quorum Queues.
    /// </summary>
    public bool UseQuorumQueues { get; set; } = false;
}
