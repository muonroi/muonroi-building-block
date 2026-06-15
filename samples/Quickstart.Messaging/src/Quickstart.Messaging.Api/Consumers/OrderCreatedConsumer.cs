using MassTransit;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Messaging.MassTransit.Messaging;
using Quickstart.Messaging.Api.Messages;

namespace Quickstart.Messaging.Api.Consumers;

/// <summary>
/// Handles <see cref="OrderCreatedEvent"/> messages.
/// Demonstrates how to extend <see cref="MuonroiConsumerBase{TMessage}"/> to receive
/// structured logging and execution-context (tenant, user) injection for free.
/// </summary>
public class OrderCreatedConsumer(
    ISystemExecutionContextAccessor contextAccessor,
    IMLog<OrderCreatedEvent> log)
    : MuonroiConsumerBase<OrderCreatedEvent>(contextAccessor, log)
{
    protected override Task HandleAsync(
        ConsumeContext<OrderCreatedEvent> context,
        ISystemExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        OrderCreatedEvent ev = context.Message;

        // Log with tenant context automatically available from executionContext.
        Log.Info(
            "Order received — OrderId: {OrderId}, Product: {Product}, Total: {Total:C}, " +
            "Tenant: {TenantId}, ContextTenant: {ContextTenant}",
            ev.OrderId,
            ev.Product,
            ev.Total,
            ev.TenantId,
            executionContext.TenantId);

        // TODO: persist order, trigger fulfilment workflow, etc.
        return Task.CompletedTask;
    }
}
