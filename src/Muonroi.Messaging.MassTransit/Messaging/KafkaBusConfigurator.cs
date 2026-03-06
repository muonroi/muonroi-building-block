namespace Muonroi.Messaging.MassTransit.Messaging;

public class KafkaBusConfigurator : IBusConfigurator
{
    public void Configure(IBusRegistrationConfigurator configurator, MessageBusConfigs configs)
    {
        KafkaConfigs kafka = configs.Kafka ?? throw new InvalidDataException("Kafka configuration missing");
        if (!string.IsNullOrWhiteSpace(configs.Runtime.EndpointPrefix))
        {
            configurator.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(configs.Runtime.EndpointPrefix, false));
        }
        else
        {
            configurator.SetKebabCaseEndpointNameFormatter();
        }

        configurator.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

        configurator.AddRider(rider =>
        {
            rider.UsingKafka((_, k) =>
            {
                k.Host(kafka.Host);
            });
        });
    }
}
