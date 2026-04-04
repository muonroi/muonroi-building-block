using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.Messaging.Abstractions.Events;

/// <summary>
/// Represents the Event Outbox.
/// </summary>
public class EventOutbox : MEntity
{
    /// <summary>
    /// Gets or sets the Event Name.
    /// </summary>
    public string? EventName { get; set; }
    /// <summary>
    /// Gets or sets the Event Content.
    /// </summary>
    public string? EventContent { get; set; }
    /// <summary>
    /// Gets or sets the Event Type.
    /// </summary>
    public string? EventType { get; set; }
    /// <summary>
    /// Gets or sets the Status.
    /// </summary>
    public EventOutboxStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the Error Message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents the Event Outbox Status.
/// </summary>
public enum EventOutboxStatus
{
    /// <summary>
    /// Represents the Pending value.
    /// </summary>
    Pending = 0,
    /// <summary>
    /// Represents the Published value.
    /// </summary>
    Published = 1,
    /// <summary>
    /// Represents the Failed value.
    /// </summary>
    Failed = 2
}

/// <summary>
/// Represents the IEvent Outbox Store.
/// </summary>
public interface IEventOutboxStore
{
    /// <summary>
    /// Gets the Event Outboxes.
    /// </summary>
    IQueryable<EventOutbox> EventOutboxes { get; }
    /// <summary>
    /// Executes the Add Async operation.
    /// </summary>
    Task AddAsync(EventOutbox outbox, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Save Changes Async operation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
