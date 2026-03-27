namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Kafka Health Check.
/// </summary>
public class KafkaHealthCheck(KafkaConfigs cfg) : IHealthCheck
{
    /// <summary>
    /// Executes the Check Health Async operation.
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AdminClientConfig adminConfig = new()
            {
                BootstrapServers = cfg.Host,
                ClientId = cfg.ClientId
            };

            if (!string.IsNullOrWhiteSpace(cfg.SecurityProtocol) &&
                Enum.TryParse(cfg.SecurityProtocol, true, out SecurityProtocol protocol))
            {
                adminConfig.SecurityProtocol = protocol;
            }

            if (!string.IsNullOrWhiteSpace(cfg.SaslMechanism) &&
                Enum.TryParse(cfg.SaslMechanism, true, out SaslMechanism mechanism))
            {
                adminConfig.SaslMechanism = mechanism;
            }

            if (!string.IsNullOrWhiteSpace(cfg.SaslUsername))
            {
                adminConfig.SaslUsername = cfg.SaslUsername;
            }

            if (!string.IsNullOrWhiteSpace(cfg.SaslPassword))
            {
                adminConfig.SaslPassword = cfg.SaslPassword;
            }

            using IAdminClient client = new AdminClientBuilder(adminConfig).Build();
            client.GetMetadata(TimeSpan.FromSeconds(5));
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ex.Message, ex));
        }
    }
}
