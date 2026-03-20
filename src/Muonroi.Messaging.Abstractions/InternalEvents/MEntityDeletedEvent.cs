namespace Muonroi.Messaging.Abstractions.InternalEvents;

/// <summary>
/// Represents the MEntity Deleted Event.
/// </summary>
public class MEntityDeletedEvent<T>(T entity) : Muonroi.Core.Abstractions.SeedWorks.INotification where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}
