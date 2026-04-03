namespace Muonroi.Messaging.Abstractions.Events;

/// <summary>
/// Represents an integration event to be published to other services.
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>
    /// Event id used for outbox persistence.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Occurrence time in UTC.
    /// </summary>
    public DateTime OccurredOn { get; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}
