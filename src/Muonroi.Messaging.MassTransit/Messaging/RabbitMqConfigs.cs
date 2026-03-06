namespace Muonroi.Messaging.MassTransit.Messaging;

public class RabbitMqConfigs
{
    public string Host { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public bool UseSsl { get; set; }

    public string SslServerName { get; set; } = string.Empty;

    public int HeartbeatSeconds { get; set; } = 30;

    public bool PublisherConfirmation { get; set; } = true;
}
