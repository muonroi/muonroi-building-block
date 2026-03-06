namespace Muonroi.Messaging.Abstractions.InternalEvents;

public class MEntityChangedEvent<T>(T entity) : Mediator.Mediator.Interfaces.INotification where T : MEntity
{
    public T Data { get; set; } = entity;
}
