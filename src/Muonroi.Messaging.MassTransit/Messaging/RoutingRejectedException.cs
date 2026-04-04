using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents a routing decision that explicitly rejects a message.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RoutingRejectedException"/> class.
/// </remarks>
/// <param name="reason">The rejection reason supplied by the routing rule.</param>
public sealed class RoutingRejectedException(string? reason) : MException("ROUTING_REJECTED", string.IsNullOrWhiteSpace(reason) ? "Message rejected by routing rule." : reason, MExceptionCategory.Domain, 400)
{
}
