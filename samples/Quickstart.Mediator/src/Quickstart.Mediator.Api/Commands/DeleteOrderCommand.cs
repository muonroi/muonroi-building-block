using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Attributes;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Quickstart.Mediator.Api.Commands;

/// <summary>
/// Command that deletes an order. Decorated with <see cref="MAuthorizeAttribute"/> so
/// <c>MAuthorizationBehavior</c> will reject the request unless the caller has the
/// "orders:delete" permission in their <c>ISystemExecutionContext.Permissions</c>.
///
/// Pass the permission via the <c>X-User-Id</c> and <c>X-Permissions</c> headers
/// (the middleware in Program.cs populates the context from request headers).
/// </summary>
[MAuthorize(Permissions = "orders:delete")]
public sealed class DeleteOrderCommand(Guid orderId) : IRequest
{
    public Guid OrderId { get; } = orderId;
}

/// <summary>
/// Handles <see cref="DeleteOrderCommand"/> — removes the order from the in-memory store.
/// </summary>
public sealed class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand>
{
    public Task<Unit> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        CreateOrderCommandHandler.Orders.Remove(request.OrderId);
        return Unit.Task;
    }
}
