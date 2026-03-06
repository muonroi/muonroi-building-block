namespace Muonroi.Messaging.Abstractions.InternalEvents;

public class MEntitiesDeletedEvent<T>(IEnumerable<T> entities) : Mediator.Mediator.Interfaces.INotification where T : MEntity
{
    public IEnumerable<T> Data { get; set; } = entities;
}
