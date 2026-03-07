namespace Muonroi.Messaging.MassTransit.Messaging;

public class RabbitMqBusConfigurator : IBusConfigurator
{
    public void Configure(IBusRegistrationConfigurator configurator, MessageBusConfigs configs)
    {
        RabbitMqConfigs rabbit = configs.RabbitMq ?? throw new InvalidDataException("RabbitMQ configuration missing");
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
