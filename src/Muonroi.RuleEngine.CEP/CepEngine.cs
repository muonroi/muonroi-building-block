namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Basic CEP engine supporting sliding/tumbling windows with TTL and out-of-order handling.
/// Events are correlated using a key.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
/// <remarks>
/// Creates a CEP engine.
/// </remarks>
/// <param name="windowSize">Size of each window.</param>
/// <param name="windowType">Windowing strategy.</param>
/// <param name="ttl">Time to live for events. Older events are discarded. Defaults to window size.</param>
public sealed class CepEngine<T>(
    TimeSpan windowSize,
    WindowType windowType,
    TimeSpan? ttl = null)
{
    private readonly TimeSpan _ttl = ttl ?? windowSize;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<CepEvent<T>>> _events = new();

    /// <summary>
    /// Adds a new event and returns events currently present in the active window for the key.
    /// </summary>
    public IReadOnlyList<CepEvent<T>> AddEvent(string key, T value, DateTime timestamp)
    {
        List<CepEvent<T>> list = _events.GetOrAdd(key, _ => []);
        lock (list)
        {
            DateTime expireBefore = timestamp - _ttl;
            list.RemoveAll(e => e.Timestamp < expireBefore);

            // insert keeping ordering by timestamp for out-of-order handling
            CepEvent<T> evt = new(key, timestamp, value);
            int idx = list.BinarySearch(evt, EventComparer.Instance);
            if (idx < 0) idx = ~idx;
            list.Insert(idx, evt);

            return windowType == WindowType.Sliding
                ? GetSliding(list, timestamp)
                : GetTumbling(list, timestamp);
        }
    }

    private List<CepEvent<T>> GetSliding(List<CepEvent<T>> list, DateTime current)
    {
        DateTime start = current - windowSize;
        return [.. list.Where(e => e.Timestamp > start && e.Timestamp <= current)];
    }

    private List<CepEvent<T>> GetTumbling(List<CepEvent<T>> list, DateTime current)
    {
        long windowTicks = windowSize.Ticks;
        long startTicks = current.Ticks / windowTicks * windowTicks;
        DateTime start = new(startTicks, DateTimeKind.Utc);
        DateTime end = start + windowSize;
        return [.. list.Where(e => e.Timestamp >= start && e.Timestamp < end)];
    }

    private sealed class EventComparer : IComparer<CepEvent<T>>
    {
        public static readonly EventComparer Instance = new();

        public int Compare(CepEvent<T>? x, CepEvent<T>? y)
        {
            return x!.Timestamp.CompareTo(y!.Timestamp);
        }
    }
}