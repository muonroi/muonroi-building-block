namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents a message router that can return an explicit routing decision.
/// </summary>
/// <typeparam name="TMessage">The message type being routed.</typeparam>
public interface IMessageRouter<TMessage>
    where TMessage : class
{
    /// <summary>
    /// Gets the execution order for this router.
    /// Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the unique router code used for diagnostics and tracing.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Evaluates the message and returns an explicit routing decision.
    /// </summary>
    /// <param name="message">The message being routed.</param>
    /// <param name="context">The ambient routing context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A routing decision for the current message.</returns>
    Task<IRoutingDecision> RouteAsync(
        TMessage message,
        IRoutingContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Provides execution metadata to a message router.
/// </summary>
public interface IRoutingContext
{
    /// <summary>
    /// Gets the tenant identifier associated with the current message.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the current message.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the fully qualified message type name.
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Gets the normalized transport headers for the current message.
    /// </summary>
    IReadOnlyDictionary<string, string> Headers { get; }
}
