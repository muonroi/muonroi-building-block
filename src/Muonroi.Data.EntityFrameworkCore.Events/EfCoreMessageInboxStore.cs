using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Messaging.Abstractions.Contracts;

namespace Muonroi.Data.EntityFrameworkCore.Events;

/// <summary>
/// Entity Framework Core implementation of the message inbox store.
/// </summary>
public class EfCoreMessageInboxStore(MEventOutboxDbContext dbContext) : IMessageInboxStore
{
    /// <inheritdoc/>
    public async Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
    {
        return await dbContext.MessageInbox
            .AnyAsync(x => x.MessageId == messageId && x.ConsumerName == consumerName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RecordProcessedAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
    {
        dbContext.MessageInbox.Add(new MessageInbox
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAt = DateTime.UtcNow
        });
        
        // Note: We don't call SaveChangesAsync here because it should be part of the ambient transaction
        // handled by the consumer's DbContext.
        await Task.CompletedTask;
    }
}
