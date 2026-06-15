using MassTransit;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Messaging.MassTransit.Messaging;
using Quickstart.Messaging.Api.Messages;

namespace Quickstart.Messaging.Api.Consumers;

/// <summary>
/// Handles <see cref="OrderShippedEvent"/> messages.
/// Demonstrates tenant-aware processing: the shipping notification could be
/// forwarded to different webhook endpoints depending on <paramref name="executionContext"/>.
/// </summary>
public class OrderShippedConsumer(
    ISystemExecutionContextAccessor contextAccessor,
    IMLog<OrderShippedEvent> log)
    : MuonroiConsumerBase<OrderShippedEvent>(contextAccessor, log)
{
    protected override Task HandleAsync(
        ConsumeContext<OrderShippedEvent> context,
        ISystemExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        OrderShippedEvent ev = context.Message;

        Log.Info(
            "Order shipped — OrderId: {OrderId}, Tracking: {TrackingNumber}, " +
            "ShippedAt: {ShippedAt:O}, Tenant: {TenantId}",
            ev.OrderId,
            ev.TrackingNumber,
            ev.ShippedAt,
            executionContext.TenantId);

        // TODO: notify customer, update fulfilment record, etc.
        return Task.CompletedTask;
    }
}
