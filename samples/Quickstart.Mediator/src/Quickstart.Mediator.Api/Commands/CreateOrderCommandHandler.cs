using Muonroi.Mediator.Mediator.Interfaces;
using Quickstart.Mediator.Api.Models;
using Quickstart.Mediator.Api.Notifications;

namespace Quickstart.Mediator.Api.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/>: persists the order in the in-memory store
/// and publishes an <see cref="OrderCreatedNotification"/> to fan-out to audit and email handlers.
/// </summary>
public sealed class CreateOrderCommandHandler(IMediator mediator) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    // Shared in-memory store used by both command and query handlers.
    internal static readonly Dictionary<Guid, OrderDto> Orders = [];

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        OrderDto order = new(
            Id: Guid.NewGuid(),
            ProductName: request.ProductName,
            Quantity: request.Quantity,
            UnitPrice: request.UnitPrice,
            Status: "Pending",
            CreatedAt: DateTimeOffset.UtcNow);

        Orders[order.Id] = order;

        // Fan-out: both AuditNotificationHandler and EmailNotificationHandler will be invoked.
        await mediator.Publish(new OrderCreatedNotification(order.Id, order.ProductName, order.CreatedAt), cancellationToken);

        return order;
    }
}
