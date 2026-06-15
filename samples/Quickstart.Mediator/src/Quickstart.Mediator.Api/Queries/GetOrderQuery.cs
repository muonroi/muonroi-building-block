using Muonroi.Mediator.Mediator.Interfaces;
using Quickstart.Mediator.Api.Models;

namespace Quickstart.Mediator.Api.Queries;

/// <summary>
/// Query that returns a single order by its identifier.
/// Implements <see cref="IRequest{OrderDto}"/> — read-only, no side effects.
/// </summary>
public sealed class GetOrderQuery(Guid orderId) : IRequest<OrderDto>
{
    public Guid OrderId { get; } = orderId;
}
