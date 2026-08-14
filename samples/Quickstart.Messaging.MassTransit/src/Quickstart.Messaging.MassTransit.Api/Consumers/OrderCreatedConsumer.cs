namespace Quickstart.Messaging.MassTransit.Api.Consumers;

public class OrderCreatedConsumer(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogger<OrderCreatedConsumer> logger) 
    : MuonroiConsumerBase<OrderCreatedEvent>(executionContextAccessor, tenantContextPolicy, logger)
{
    protected override Task ConsumeMessageAsync(ConsumeContext<OrderCreatedEvent> context)
    {
        var execContext = ExecutionContextAccessor.Get();
        Logger.LogInformation("Order {OrderId} created for tenant {TenantId}. Customer: {CustomerName}",
            context.Message.OrderId, execContext?.TenantId, context.Message.CustomerName);
            
        return Task.CompletedTask;
    }
}
