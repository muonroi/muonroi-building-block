namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntity Changed Event.
/// </summary>
public class MEntityChangedEvent<T>(T entity) : Mediator.Mediator.Interfaces.INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}
