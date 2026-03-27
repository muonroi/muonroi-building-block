using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Rabbit Mq Health Check.
/// </summary>
public class RabbitMqHealthCheck(RabbitMqConfigs configs) : IHealthCheck
{
    /// <summary>
    /// Executes the Check Health Async operation.
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ConnectionFactory factory = new()
            {
                HostName = configs.Host,
                UserName = configs.Username,
                Password = configs.Password,
                VirtualHost = configs.VirtualHost,
                Port = configs.Port
            };
            using IConnection connection = factory.CreateConnection();
            return Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ is connected")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ is down", ex));
        }
    }
}
