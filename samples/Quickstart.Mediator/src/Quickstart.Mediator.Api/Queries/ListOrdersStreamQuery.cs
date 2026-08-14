namespace Quickstart.Mediator.Api.Queries;

/// <summary>
/// Stream query that yields a paginated slice of orders one at a time.
/// Implements <see cref="IStreamRequest{OrderDto}"/> — the mediator calls
/// <see cref="ListOrdersStreamHandler"/> and returns an <see cref="IAsyncEnumerable{OrderDto}"/>.
/// </summary>
public sealed class ListOrdersStreamQuery(int count) : IStreamRequest<OrderDto>
{
    /// <summary>Maximum number of orders to stream. Defaults to all.</summary>
    public int Count { get; } = count;
}
