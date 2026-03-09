using System.Diagnostics;
using System.Diagnostics.Metrics;
using Muonroi.RuleEngine.CEP.Observability;

namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Basic CEP engine supporting sliding and tumbling windows with TTL and out-of-order handling.
/// Events are correlated using a key.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public sealed class CepEngine<T>
{
    private readonly TimeSpan _ttl;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<CepEvent<T>>> _events = new();

    /// <summary>
    /// Creates a CEP engine.
    /// </summary>
    public CepEngine(
        TimeSpan windowSize,
        WindowType windowType,
        TimeSpan? ttl = null)
    {
        if (windowSize <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be greater than zero.");
        }

        if (ttl.HasValue && ttl.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero when provided.");
        }

        WindowSize = windowSize;
        WindowType = windowType;
        _ttl = ttl ?? windowSize;
    }

    /// <summary>
    /// Gets the configured window size.
    /// </summary>
    public TimeSpan WindowSize { get; }

    /// <summary>
    /// Gets the configured window type.
    /// </summary>
    public WindowType WindowType { get; }

    /// <summary>
    /// Adds a new event and returns events currently present in the active window for the key.
    /// </summary>
    public IReadOnlyList<CepEvent<T>> AddEvent(
        string key,
        T value,
        DateTime timestamp,
        string? configId = null,
        string? tenantId = null)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(key) ? "default" : key.Trim();
        DateTime utcTimestamp = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();

        using Activity? activity = CepMetrics.ActivitySource.StartActivity("cep.window.evaluate", ActivityKind.Internal);
        activity?.SetTag("cep.key", normalizedKey);
        activity?.SetTag("cep.window.type", WindowType.ToString());
        activity?.SetTag("cep.window.size.ms", WindowSize.TotalMilliseconds);
        if (!string.IsNullOrWhiteSpace(configId))
        {
            activity?.SetTag("cep.config.id", configId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            activity?.SetTag("tenant.id", tenantId);
        }

        List<CepEvent<T>> list = _events.GetOrAdd(normalizedKey, _ => []);
        lock (list)
        {
            DateTime expireBefore = utcTimestamp - _ttl;
            list.RemoveAll(e => e.Timestamp < expireBefore);

            CepEvent<T> evt = new(normalizedKey, utcTimestamp, value);
            int idx = list.BinarySearch(evt, EventComparer.Instance);
            if (idx < 0)
            {
                idx = ~idx;
            }

            list.Insert(idx, evt);

            IReadOnlyList<CepEvent<T>> window = WindowType == WindowType.Sliding
                ? GetSliding(list, utcTimestamp)
                : GetTumbling(list, utcTimestamp);

            TagList tags = new()
            {
                { "cep.window.type", WindowType.ToString().ToLowerInvariant() }
            };
            if (!string.IsNullOrWhiteSpace(configId))
            {
                tags.Add("cep.config.id", configId);
            }

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                tags.Add("tenant.id", tenantId);
            }

            CepMetrics.EventsProcessed.Add(1, tags);
            CepMetrics.WindowEvaluations.Add(1, tags);
            CepMetrics.WindowEventCount.Record(window.Count, tags);
            activity?.SetTag("cep.window.event_count", window.Count);

            return window;
        }
    }

    private List<CepEvent<T>> GetSliding(List<CepEvent<T>> list, DateTime current)
    {
        DateTime start = current - WindowSize;
        return [.. list.Where(e => e.Timestamp > start && e.Timestamp <= current)];
    }

    private List<CepEvent<T>> GetTumbling(List<CepEvent<T>> list, DateTime current)
    {
        long windowTicks = WindowSize.Ticks;
        long startTicks = current.Ticks / windowTicks * windowTicks;
        DateTime start = new(startTicks, DateTimeKind.Utc);
        DateTime end = start + WindowSize;
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
