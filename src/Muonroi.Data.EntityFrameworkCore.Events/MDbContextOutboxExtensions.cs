using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Data.EntityFrameworkCore;

/// <summary>
/// Provides outbox helpers for <see cref="MEventOutboxDbContext"/>.
/// </summary>
public static class MDbContextOutboxExtensions
{
    /// <summary>
    /// Saves changes and writes the integration event to the outbox.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    /// <param name="dbContext">The outbox-aware DbContext.</param>
    /// <param name="integrationEvent">The event to persist.</param>
    /// <param name="jsonService">JSON serializer for the event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public static async Task<int> SaveWithOutboxAsync<TEvent>(
        this MEventOutboxDbContext dbContext,
        TEvent integrationEvent,
        IMJsonSerializeService jsonService,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        string eventContent = jsonService.Serialize(integrationEvent);
        Type eventType = integrationEvent.GetType();

        EventOutbox outbox = new()
        {
            EventName = eventType.Name,
            EventType = eventType.AssemblyQualifiedName ?? eventType.FullName ?? eventType.Name,
            EventContent = eventContent,
            Status = EventOutboxStatus.Pending
        };

        await dbContext.AddAsync(outbox, cancellationToken);
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
