namespace Muonroi.Rules.Rules;

/// <summary>
/// Publishes and subscribes ruleset change events for hot-reload.
/// </summary>
public interface IRuleSetChangeNotifier
{
    Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default);

    IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler);
}
