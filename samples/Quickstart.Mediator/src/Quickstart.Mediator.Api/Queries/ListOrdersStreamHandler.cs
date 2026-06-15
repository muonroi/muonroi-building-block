using Muonroi.Mediator.Mediator.Interfaces;
using Quickstart.Mediator.Api.Commands;
using Quickstart.Mediator.Api.Models;
using System.Runtime.CompilerServices;

namespace Quickstart.Mediator.Api.Queries;

/// <summary>
/// Handles <see cref="ListOrdersStreamQuery"/> by streaming orders from the in-memory store.
/// Demonstrates <see cref="IStreamRequestHandler{TRequest,MResponse}"/> — each item is yielded
/// independently, enabling true streaming over HTTP via IAsyncEnumerable.
/// </summary>
public sealed class ListOrdersStreamHandler : IStreamRequestHandler<ListOrdersStreamQuery, OrderDto>
{
    public async IAsyncEnumerable<OrderDto> Handle(
        ListOrdersStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IEnumerable<OrderDto> slice = CreateOrderCommandHandler.Orders.Values
            .Take(request.Count > 0 ? request.Count : int.MaxValue);

        foreach (OrderDto order in slice)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate async work (e.g., lazy loading from a cursor).
            await Task.Delay(10, cancellationToken);
            yield return order;
        }
    }
}
