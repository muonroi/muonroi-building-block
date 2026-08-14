namespace Muonroi.Rules.Rules;

/// <summary>
/// In-process notifier used when no distributed transport is configured.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class InMemoryRuleSetChangeNotifier : IRuleSetChangeNotifier
{
    private readonly ConcurrentDictionary<Guid, Func<RuleSetChangeEvent, Task>> _handlers = new();

    /// <summary>
    /// Publishes a ruleset change event to all subscribers.
    /// </summary>
    /// <param name="changeEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous publication.</returns>
    public async Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
    {
        foreach (Func<RuleSetChangeEvent, Task> handler in _handlers.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler(changeEvent);
        }
    }

    /// <summary>
    /// Subscribes to ruleset change events.
    /// </summary>
    /// <param name="handler">The handler to invoke when a change event occurs.</param>
    /// <returns>An <see cref="IDisposable"/> instance that can be used to unsubscribe.</returns>
    public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
    {
        Guid id = Guid.NewGuid();
        _handlers[id] = handler;
        return new Subscription(() => _handlers.TryRemove(id, out _));
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onDispose();
            }
        }
    }
}
