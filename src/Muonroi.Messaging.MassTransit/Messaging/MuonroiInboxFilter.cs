using System;
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

        if (!context.TryGetPayload<IServiceProvider>(out var serviceProvider))
        {
            // If DI container is not available in payload, we can't do inbox dedup
            await next.Send(context);
            return;
        }

        var inboxStore = serviceProvider.GetService(typeof(Muonroi.Messaging.Abstractions.Contracts.IMessageInboxStore)) as Muonroi.Messaging.Abstractions.Contracts.IMessageInboxStore;
        if (inboxStore == null)
        {
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
        var alreadyProcessed = await inboxStore.HasBeenProcessedAsync(messageId, consumerType.Name);

        if (alreadyProcessed)
        {
            logger.Info("Message {MessageId} already processed by {ConsumerName}. Skipping.",
                messageId, consumerType.Name);
            return;
        }

        // Process message
        await next.Send(context);

        // Record in inbox (must be in same transaction as consumer changes)
        await inboxStore.RecordProcessedAsync(messageId, consumerType.Name);
    }

    /// <inheritdoc/>
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("muonroi-inbox");
    }
}
