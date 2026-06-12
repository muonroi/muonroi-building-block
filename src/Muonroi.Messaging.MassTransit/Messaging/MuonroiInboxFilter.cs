using Microsoft.EntityFrameworkCore;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Messaging.Abstractions.Attributes;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// MassTransit filter that implements the inbox pattern for consumer deduplication.
/// </summary>
public class MuonroiInboxFilter<TConsumer, TMessage>(IMLog<MuonroiInboxFilter<TConsumer, TMessage>> logger)
    : IFilter<ConsumerConsumeContext<TConsumer, TMessage>>
    where TConsumer : class
    where TMessage : class
{
    /// <summary>
    /// Intercepts the message consumption pipeline to implement the inbox pattern for idempotent consumers.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task Send(ConsumerConsumeContext<TConsumer, TMessage> context, IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
    {
        var consumerType = typeof(TConsumer);
        var isIdempotent = consumerType.GetCustomAttribute<IdempotentAttribute>() != null;

        if (!isIdempotent)
        {
            await next.Send(context);
            return;
        }

        if (!context.TryGetPayload<MEventOutboxDbContext>(out var dbContext))
        {
            // If DB context is not available in payload, we can't do inbox dedup
            await next.Send(context);
            return;
        }

        var messageId = context.MessageId ?? Guid.Empty;

        if (messageId == Guid.Empty)
        {
            await next.Send(context);
            return;
        }

        // Check if message already processed (D-07)
        var alreadyProcessed = await dbContext.MessageInbox
            .AnyAsync(x => x.MessageId == messageId && x.ConsumerName == consumerType.Name);

        if (alreadyProcessed)
        {
            logger.Info("Message {MessageId} already processed by {ConsumerName}. Skipping.",
                messageId, consumerType.Name);
            return;
        }

        // Process message
        await next.Send(context);

        // Record in inbox (must be in same transaction as consumer changes)
        dbContext.MessageInbox.Add(new MessageInbox
        {
            MessageId = messageId,
            ConsumerName = consumerType.Name,
            ProcessedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc/>
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("muonroi-inbox");
    }
}
