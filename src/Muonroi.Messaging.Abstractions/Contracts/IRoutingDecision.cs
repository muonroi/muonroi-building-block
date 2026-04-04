namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents the outcome returned by a message router.
/// </summary>
public interface IRoutingDecision
{
    /// <summary>
    /// Gets the target transport address.
    /// A null value keeps the current transport routing unchanged.
    /// </summary>
    string? TargetAddress { get; }

    /// <summary>
    /// Gets a value indicating whether the message should be rejected.
    /// </summary>
    bool Reject { get; }

    /// <summary>
    /// Gets the human-readable reason attached to the routing outcome.
    /// </summary>
    string? Reason { get; }
}

/// <summary>
/// Default immutable implementation of <see cref="IRoutingDecision"/>.
/// </summary>
/// <param name="TargetAddress">The target transport address, when redirection is requested.</param>
/// <param name="Reject">True to reject the message and send it to the fault/error transport.</param>
/// <param name="Reason">Optional human-readable reason for the routing outcome.</param>
public sealed record RoutingDecision(
    string? TargetAddress = null,
    bool Reject = false,
    string? Reason = null) : IRoutingDecision
{
    /// <summary>
    /// Gets a pass-through routing decision.
    /// </summary>
    public static readonly IRoutingDecision PassThrough = new RoutingDecision();

    /// <summary>
    /// Creates a redirect decision for the supplied transport address.
    /// </summary>
    /// <param name="address">The destination transport address.</param>
    /// <param name="reason">Optional human-readable reason.</param>
    /// <returns>A redirect decision.</returns>
    public static IRoutingDecision RedirectTo(string address, string? reason = null)
    {
        return new RoutingDecision(TargetAddress: address, Reason: reason);
    }

    /// <summary>
    /// Creates a rejection decision that moves the message to the fault/error transport.
    /// </summary>
    /// <param name="reason">Human-readable rejection reason.</param>
    /// <returns>A rejection decision.</returns>
    public static IRoutingDecision DeadLetter(string reason)
    {
        return new RoutingDecision(Reject: true, Reason: reason);
    }
}
