using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.Messaging.Abstractions.Events;

public class EventOutbox : MEntity
{
    public string? EventName { get; set; }
    public string? EventContent { get; set; }
    public string? EventType { get; set; }
    public EventOutboxStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum EventOutboxStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}

public interface IEventOutboxStore
{
    IQueryable<EventOutbox> EventOutboxes { get; }
    Task AddAsync(EventOutbox outbox, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
