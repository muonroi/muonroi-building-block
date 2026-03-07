using Confluent.Kafka;

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

        // Note: MassTransit Riders (like Kafka) require a base transport bus to handle internal mechanics
        // like sagas, outbox, and fault handling. If the application is fully Kafka-centric, UsingInMemory
        // is the correct, standard approach for providing this base bus.
        configurator.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

        configurator.AddRider(rider =>
        {
            rider.UsingKafka((_, k) =>
            {
                k.Host(kafka.Host, h =>
                {
                    if (!string.IsNullOrWhiteSpace(kafka.SecurityProtocol) && Enum.TryParse(kafka.SecurityProtocol, true, out SecurityProtocol protocol))
                    {
                        h.UseSasl(s =>
                        {
                            s.SecurityProtocol = protocol;
                            
                            if (Enum.TryParse(kafka.SaslMechanism, true, out SaslMechanism mechanism))
                            {
                                s.Mechanism = mechanism;
                            }
                            
                            if (!string.IsNullOrWhiteSpace(kafka.SaslUsername))
                            {
                                s.Username = kafka.SaslUsername;
                                s.Password = kafka.SaslPassword;
                            }
                        });
                    }
                });

                k.ClientId = kafka.ClientId;
                // Currently MassTransit doesn't expose EnableAutoCommit directly at the root `k` configurator easily in newer versions, 
                // but we can configure it per consumer topic if needed or let it use defaults.
            });
        });
    }
}
