namespace Muonroi.Messaging.Abstractions.InternalEvents;

public class MEntityDeletedEvent<T>(T entity) : Muonroi.Core.Abstractions.SeedWorks.INotification where T : MEntity
{
    public T Data { get; set; } = entity;
}
