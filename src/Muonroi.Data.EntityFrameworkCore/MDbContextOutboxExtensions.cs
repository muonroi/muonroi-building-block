using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Messaging.Abstractions.Events;

namespace Muonroi.Data.EntityFrameworkCore;

public static class MDbContextOutboxExtensions
{
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
