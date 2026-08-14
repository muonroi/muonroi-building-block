namespace Quickstart.Messaging.MassTransit.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController(IPublishEndpoint publishEndpoint, ISystemExecutionContextAccessor contextAccessor) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromQuery] string tenantId = "T-777", [FromQuery] string customerName = "Alice")
    {
        // Setup context for the current request
        contextAccessor.Set(new DefaultSystemExecutionContext { TenantId = tenantId, CorrelationId = Guid.NewGuid().ToString() });
        
        var orderEvent = new OrderCreatedEvent(Guid.NewGuid(), customerName)
        {
            TenantId = tenantId,
            CorrelationId = contextAccessor.Get()?.CorrelationId
        };
        
        // Publish event (in real app, use outbox)
        await publishEndpoint.Publish(orderEvent);
        
        return Ok(new { Message = "Order created and event published", EventId = orderEvent.Id });
    }
}
