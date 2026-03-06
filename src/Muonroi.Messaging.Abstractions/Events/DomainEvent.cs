namespace Muonroi.Messaging.Abstractions.Events;

/// <summary>
/// Represents a domain event that is handled inside the same bounded context.
/// </summary>
public abstract class DomainEvent : Mediator.Mediator.Interfaces.INotification
{
    /// <summary>
    /// Gets the date the event occurred.
    /// </summary>
    public DateTime OccurredOn { get; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}
