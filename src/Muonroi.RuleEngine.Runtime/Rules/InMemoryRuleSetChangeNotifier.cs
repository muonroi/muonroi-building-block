namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// In-process notifier used when no distributed transport is configured.
/// </summary>
public sealed class InMemoryRuleSetChangeNotifier : IRuleSetChangeNotifier
{
    private readonly ConcurrentDictionary<Guid, Func<RuleSetChangeEvent, Task>> _handlers = new();

    public async Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
    {
        foreach (Func<RuleSetChangeEvent, Task> handler in _handlers.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler(changeEvent);
        }
    }

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
