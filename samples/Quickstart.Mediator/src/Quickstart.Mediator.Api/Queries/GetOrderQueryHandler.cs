using Muonroi.Mediator.Mediator.Interfaces;
using Quickstart.Mediator.Api.Commands;
using Quickstart.Mediator.Api.Models;

namespace Quickstart.Mediator.Api.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by looking up the order in the shared in-memory store.
/// </summary>
public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    public Task<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        if (!CreateOrderCommandHandler.Orders.TryGetValue(request.OrderId, out OrderDto? order))
        {
            throw new KeyNotFoundException($"Order '{request.OrderId}' was not found.");
        }

        return Task.FromResult(order);
    }
}
