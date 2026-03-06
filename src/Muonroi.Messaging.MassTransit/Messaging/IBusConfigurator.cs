namespace Muonroi.Messaging.MassTransit.Messaging;

public interface IBusConfigurator
{
    void Configure(IBusRegistrationConfigurator configurator, MessageBusConfigs configs);
}
