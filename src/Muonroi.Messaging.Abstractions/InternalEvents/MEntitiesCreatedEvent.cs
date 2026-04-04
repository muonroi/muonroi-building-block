namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntities Created Event.
/// </summary>
public class MEntitiesCreatedEvent<T>(IEnumerable<T> entities) : INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}
