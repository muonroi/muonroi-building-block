using Muonroi.Mediator.Mediator.Interfaces;
using Quickstart.Mediator.Api.Models;

namespace Quickstart.Mediator.Api.Commands;

/// <summary>
/// Command to create a new order. Implements IRequest&lt;OrderDto&gt; so the mediator
/// dispatches it to exactly one <see cref="CreateOrderCommandHandler"/>.
/// </summary>
public sealed class CreateOrderCommand : IRequest<OrderDto>
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
