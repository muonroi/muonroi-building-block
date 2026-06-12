namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntity Created Event.
/// </summary>
public class MEntityCreatedEvent<T>(T entity) : INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}
