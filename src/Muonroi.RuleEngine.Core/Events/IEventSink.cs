namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Defines a transport-agnostic sink for publishing CloudEvents.
/// Implementations may dispatch to in-memory queues, message buses, webhooks, etc.
/// </summary>
public interface IEventSink
{
    /// <summary>
    /// Publishes a CloudEvent to the underlying transport.
    /// </summary>
    /// <param name="cloudEvent">The event envelope to publish.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    Task PublishAsync(CloudEvent cloudEvent, CancellationToken ct = default);
}
