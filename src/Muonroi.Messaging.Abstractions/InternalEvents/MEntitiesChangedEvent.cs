namespace Muonroi.Messaging.Abstractions.InternalEvents;

public class MEntitiesChangedEvent<T>(IEnumerable<T> entities) : Muonroi.Core.Abstractions.SeedWorks.INotification where T : MEntity
{
    public IEnumerable<T> Data { get; set; } = entities;
}
