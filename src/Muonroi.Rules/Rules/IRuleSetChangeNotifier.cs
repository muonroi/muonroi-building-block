namespace Muonroi.Rules.Rules;

/// <summary>
/// Publishes and subscribes ruleset change events for hot-reload.
/// </summary>
public interface IRuleSetChangeNotifier
{
    /// <summary>
    /// Publishes a ruleset change event.
    /// </summary>
    /// <param name="changeEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous publication.</returns>
    Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to ruleset change events.
    /// </summary>
    /// <param name="handler">The handler to invoke when a change event occurs.</param>
    /// <returns>An <see cref="IDisposable"/> instance that can be used to unsubscribe.</returns>
    IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler);
}
