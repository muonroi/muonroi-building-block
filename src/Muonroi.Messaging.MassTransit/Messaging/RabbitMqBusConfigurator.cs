using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Rabbit Mq Bus Configurator.
/// </summary>
public class RabbitMqBusConfigurator : IBusConfigurator
{
    /// <summary>
    /// Executes the Configure operation.
    /// </summary>
    public void Configure(IBusRegistrationConfigurator configurator, MessageBusConfigs configs)
    {
        RabbitMqConfigs rabbit = configs.RabbitMq ?? MGuard.Fail<RabbitMqConfigs>("RabbitMQ configuration missing", "MessageBus:RabbitMq");
        if (!string.IsNullOrWhiteSpace(configs.Runtime.EndpointPrefix))
        {
            configurator.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(configs.Runtime.EndpointPrefix, false));
        }
        else
        {
            configurator.SetKebabCaseEndpointNameFormatter();
        }

        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbit.Host, (ushort)rabbit.Port, rabbit.VirtualHost, h =>
            {
                h.Username(rabbit.Username);
                h.Password(rabbit.Password);
                h.Heartbeat(TimeSpan.FromSeconds(rabbit.HeartbeatSeconds));
                h.PublisherConfirmation = rabbit.PublisherConfirmation;
                
                if (rabbit.UseSsl)
                {
                    h.UseSsl(s =>
                    {
                        if (!string.IsNullOrWhiteSpace(rabbit.SslServerName))
                        {
                            s.ServerName = rabbit.SslServerName;
                        }
                    });
                }
            });

            if (rabbit.UseQuorumQueues)
            {
                cfg.SetQuorumQueue();
            }

            cfg.ConfigureEndpoints(context);
        });
    }
}
