namespace Quickstart.Messaging.MassTransit.Api.Messages;

public record OrderCreatedEvent(Guid OrderId, string CustomerName) : IntegrationEvent;
