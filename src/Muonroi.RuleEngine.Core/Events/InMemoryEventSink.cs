using System.Collections.Concurrent;

namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// An in-memory implementation of <see cref="IEventSink"/> intended for testing scenarios.
/// Events are stored in a thread-safe queue and retrievable via the <see cref="Events"/> property.
/// </summary>
public sealed class InMemoryEventSink : IEventSink
{
    private readonly ConcurrentQueue<CloudEvent> _queue = new();

    /// <summary>
    /// Returns a snapshot of all published events in the order they were received.
    /// </summary>
    public IReadOnlyList<CloudEvent> Events => _queue.ToArray();

    /// <inheritdoc />
    public Task PublishAsync(CloudEvent cloudEvent, CancellationToken ct = default)
    {
        _queue.Enqueue(cloudEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes all captured events from the internal queue.
    /// Useful for resetting state between test cases.
    /// </summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
