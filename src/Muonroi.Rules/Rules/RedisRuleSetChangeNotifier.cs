using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Redis Pub/Sub based notifier for cross-node ruleset hot reload.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class RedisRuleSetChangeNotifier : IRuleSetChangeNotifier, IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly string _channelName;
    private readonly ConcurrentDictionary<Guid, Func<RuleSetChangeEvent, Task>> _handlers = new();
    private readonly IMJsonSerializeService _jsonSerializeService;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRuleSetChangeNotifier"/> class.
    /// </summary>
    /// <param name="connection">The Redis connection multiplexer.</param>
    /// <param name="channelName">The name of the Redis channel to use for pub/sub.</param>
    /// <param name="jsonSerializeService">The JSON serialization service.</param>
    public RedisRuleSetChangeNotifier(IConnectionMultiplexer connection, string channelName, IMJsonSerializeService jsonSerializeService)
    {
        _jsonSerializeService = jsonSerializeService;
        _subscriber = connection.GetSubscriber();
        _channelName = string.IsNullOrWhiteSpace(channelName) ? "muonroi:ruleset:changed" : channelName;
        _subscriber.Subscribe(RedisChannel.Literal(_channelName), (channel, message) =>
        {
            _ = HandleMessageAsync(message);
        });
    }

    /// <summary>
    /// Publishes a ruleset change event to the Redis channel.
    /// </summary>
    /// <param name="changeEvent">The ruleset change event to publish.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string payload = _jsonSerializeService.Serialize(changeEvent);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), payload);
    }

    /// <summary>
    /// Subscribes to ruleset change events.
    /// </summary>
    /// <param name="handler">The handler function to invoke when a change event is received.</param>
    /// <returns>An <see cref="IDisposable"/> instance that can be used to unsubscribe.</returns>
    public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
    {
        Guid id = Guid.NewGuid();
        _handlers[id] = handler;
        return new Subscription(() => _handlers.TryRemove(id, out _));
    }

    private async Task HandleMessageAsync(RedisValue message)
    {
        if (message.IsNullOrEmpty || _handlers.Count == 0)
        {
            return;
        }

        RuleSetChangeEvent? changeEvent;
        try
        {
            changeEvent = _jsonSerializeService.Deserialize<RuleSetChangeEvent>(message!);
        }
        catch
        {
            return;
        }

        if (changeEvent is null)
        {
            return;
        }

        foreach (Func<RuleSetChangeEvent, Task> handler in _handlers.Values)
        {
            try
            {
                await handler(changeEvent);
            }
            catch
            {
                // ignore handler failures to keep subscriber alive
            }
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _subscriber.Unsubscribe(RedisChannel.Literal(_channelName));
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
