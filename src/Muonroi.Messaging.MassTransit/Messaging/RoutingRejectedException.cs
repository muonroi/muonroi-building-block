namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents a routing decision that explicitly rejects a message.
/// </summary>
public sealed class RoutingRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingRejectedException"/> class.
    /// </summary>
    /// <param name="reason">The rejection reason supplied by the routing rule.</param>
    public RoutingRejectedException(string? reason)
        : base(string.IsNullOrWhiteSpace(reason) ? "Message rejected by routing rule." : reason)
    {
    }
}
