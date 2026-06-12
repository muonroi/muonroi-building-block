namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntities Changed Event.
/// </summary>
public class MEntitiesChangedEvent<T>(IEnumerable<T> entities) : INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}
