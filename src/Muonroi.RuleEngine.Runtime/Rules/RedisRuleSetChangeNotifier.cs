using Muonroi.Core.Abstractions.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Redis Pub/Sub based notifier for cross-node ruleset hot reload.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Redis Pub/Sub deserialized message values are guaranteed non-null by the subscription handler contract.")]
public sealed class RedisRuleSetChangeNotifier : IRuleSetChangeNotifier, IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly string _channelName;
    private readonly ConcurrentDictionary<Guid, Func<RuleSetChangeEvent, Task>> _handlers = new();
    private readonly IMJsonSerializeService _jsonSerializeService;
    private int _disposed;

    /// <summary>Creates a Redis-backed ruleset change notifier.</summary>
    /// <param name="connection">Redis connection.</param>
    /// <param name="channelName">Pub/Sub channel name.</param>
    /// <param name="jsonSerializeService">Serializer for change events.</param>
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

    /// <inheritdoc />
    public async Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string payload = _jsonSerializeService.Serialize(changeEvent);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), payload);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
    {
        Guid id = Guid.NewGuid();
        _handlers[id] = handler;
        return new Subscription(() => _handlers.TryRemove(id, out _));
    }

    private async Task HandleMessageAsync(RedisValue message)
    {
        if (message.IsNullOrEmpty || _handlers.Count == 0) return;

        RuleSetChangeEvent? changeEvent;
        try
        {
            changeEvent = _jsonSerializeService.Deserialize<RuleSetChangeEvent>(message!);
        }
        catch
        {
            return;
        }

        if (changeEvent is null) return;

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
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
