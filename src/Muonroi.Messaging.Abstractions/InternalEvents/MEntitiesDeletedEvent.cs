namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntities Deleted Event.
/// </summary>
public class MEntitiesDeletedEvent<T>(IEnumerable<T> entities) : Mediator.Mediator.Interfaces.INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}
