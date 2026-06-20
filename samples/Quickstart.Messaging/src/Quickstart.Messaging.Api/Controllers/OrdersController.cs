using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Quickstart.Messaging.Api.Messages;

namespace Quickstart.Messaging.Api.Controllers;

/// <summary>
/// Demonstrates publishing events via MassTransit's <see cref="IPublishEndpoint"/>
/// and <see cref="IBus"/> from an ASP.NET Core controller.
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(IPublishEndpoint publishEndpoint, IBus bus) : ControllerBase
{
    /// <summary>
    /// Creates a new order and publishes an <see cref="OrderCreatedEvent"/>.
    /// Uses <see cref="IPublishEndpoint"/> — the recommended endpoint for fan-out
    /// publish from within the request scope.
    /// </summary>
    /// <param name="request">Order creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with the published event payload.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderCreatedEvent), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        OrderCreatedEvent ev = new(
            OrderId: Guid.NewGuid(),
            Product: request.Product,
            Total: request.Total,
            TenantId: request.TenantId);

        // IPublishEndpoint resolves from the DI scope and routes to all subscribers.
        await publishEndpoint.Publish(ev, cancellationToken);

        return Accepted(new
        {
            message = "Order created event published.",
            ev.OrderId,
            ev.Product,
            ev.Total,
            ev.TenantId
        });
    }

    /// <summary>
    /// Marks an order as shipped and publishes an <see cref="OrderShippedEvent"/>.
    /// Uses <see cref="IBus"/> directly to illustrate the singleton bus alternative.
    /// </summary>
    /// <param name="id">The order identifier.</param>
    /// <param name="request">Shipment details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with the published event payload.</returns>
    [HttpPost("{id:guid}/ship")]
    [ProducesResponseType(typeof(OrderShippedEvent), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ShipOrder(
        Guid id,
        [FromBody] ShipOrderRequest request,
        CancellationToken cancellationToken)
    {
        OrderShippedEvent ev = new(
            OrderId: id,
            TrackingNumber: request.TrackingNumber,
            ShippedAt: DateTimeOffset.UtcNow);

        // IBus is a singleton; fine to call directly for one-off publishes.
        await bus.Publish(ev, cancellationToken);

        return Accepted(new
        {
            message = "Order shipped event published.",
            ev.OrderId,
            ev.TrackingNumber,
            ev.ShippedAt
        });
    }
}

/// <summary>Request body for <see cref="OrdersController.CreateOrder"/>.</summary>
public record CreateOrderRequest(string Product, decimal Total, string TenantId);

/// <summary>Request body for <see cref="OrdersController.ShipOrder"/>.</summary>
public record ShipOrderRequest(string TrackingNumber);
