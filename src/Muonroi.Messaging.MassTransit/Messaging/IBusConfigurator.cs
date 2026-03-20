namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the IBus Configurator.
/// </summary>
public interface IBusConfigurator
{
    /// <summary>
    /// Executes the Configure operation.
    /// </summary>
    void Configure(IBusRegistrationConfigurator configurator, MessageBusConfigs configs);
}
