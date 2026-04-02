#if (isAggregate)
namespace MuonroiService.Core.Commands;

/// <summary>
/// Command to create a new order.
/// Aggregate-layer command — dispatched to downstream service via gRPC facade client.
/// Replace with your domain command.
/// </summary>
public record CreateOrderCommand(
    string Name,
    string Description,
    string LinerCode,
    CancellationToken CancellationToken = default);
#endif
