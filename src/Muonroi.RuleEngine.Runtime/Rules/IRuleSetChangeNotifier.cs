namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Publishes and subscribes ruleset change events for hot-reload.
/// </summary>
public interface IRuleSetChangeNotifier
{
    /// <summary>Publishes a ruleset change event.</summary>
    /// <param name="changeEvent">Change event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default);

    /// <summary>Subscribes a handler for change events.</summary>
    /// <param name="handler">Handler to invoke.</param>
    /// <returns>A disposable subscription.</returns>
    IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler);
}
