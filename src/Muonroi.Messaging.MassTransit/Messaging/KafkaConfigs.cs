namespace Muonroi.Messaging.MassTransit.Messaging;

public class KafkaConfigs
{
    public string Host { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string GroupId { get; set; } = "consumer-group";

    public string ClientId { get; set; } = "muonroi-messagebus";

    public bool EnableAutoCommit { get; set; } = true;

    public string SecurityProtocol { get; set; } = string.Empty;

    public string SaslMechanism { get; set; } = string.Empty;

    public string SaslUsername { get; set; } = string.Empty;

    public string SaslPassword { get; set; } = string.Empty;
}
