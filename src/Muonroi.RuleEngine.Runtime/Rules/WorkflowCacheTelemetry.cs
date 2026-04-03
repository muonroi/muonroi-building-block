using System.Diagnostics.Metrics;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// OpenTelemetry metrics for the in-process WorkflowCache (static ConcurrentDictionary, max 2048 entries, LRU eviction).
/// Follows the Muonroi OTel convention: meter name = "muonroi.{subsystem}.{component}".
/// </summary>
public static class WorkflowCacheTelemetry
{
    /// <summary>Meter name following Muonroi OTel conventions.</summary>
    public const string MeterName = "muonroi.ruleengine.workflowcache";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    // ---------------------------------------------------------------------------------
    // Counters
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Counter: number of WorkflowCache lookups that found a matching entry (cache hit).
    /// Dimensions: tenant_id.
    /// </summary>
    public static readonly Counter<long> HitCounter =
        _meter.CreateCounter<long>(
            "muonroi.ruleengine.workflowcache.hits",
            unit: "{hit}",
            description: "Number of WorkflowCache hits");

    /// <summary>
    /// Counter: number of WorkflowCache lookups that did not find a matching entry (cache miss).
    /// Dimensions: tenant_id.
    /// </summary>
    public static readonly Counter<long> MissCounter =
        _meter.CreateCounter<long>(
            "muonroi.ruleengine.workflowcache.misses",
            unit: "{miss}",
            description: "Number of WorkflowCache misses");

    /// <summary>
    /// Counter: number of entries evicted from WorkflowCache during LRU eviction runs.
    /// </summary>
    public static readonly Counter<long> EvictionCounter =
        _meter.CreateCounter<long>(
            "muonroi.ruleengine.workflowcache.evictions",
            unit: "{eviction}",
            description: "Number of entries evicted from WorkflowCache");

    // ---------------------------------------------------------------------------------
    // Histogram
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Histogram: time elapsed (in milliseconds) from the start of NotifyRuleChangedAsync
    /// to completion of both runtime cache invalidation and WorkflowCache TryRemove.
    /// Measures hot-reload propagation lag for SLA monitoring.
    /// </summary>
    public static readonly Histogram<double> HotReloadLagHistogram =
        _meter.CreateHistogram<double>(
            "muonroi.ruleengine.workflowcache.hotreload_lag_ms",
            unit: "ms",
            description: "Time from hot-reload notification to cache invalidation completion");

    // ---------------------------------------------------------------------------------
    // Observable gauge (current cache size)
    // ---------------------------------------------------------------------------------

    // Provider registered once by RulesEngineService static constructor.
    private static Func<int>? _cacheSizeProvider;

    /// <summary>
    /// Registers a callback that returns the current WorkflowCache entry count for the observable gauge.
    /// Idempotent — subsequent calls after the first registration are ignored.
    /// </summary>
    /// <param name="provider">Function returning current cache entry count.</param>
    public static void RegisterCacheSizeProvider(Func<int> provider)
    {
        if (_cacheSizeProvider is not null) return; // Already registered — idempotent
        _cacheSizeProvider = provider;
        _meter.CreateObservableGauge(
            "muonroi.ruleengine.workflowcache.size",
            () => _cacheSizeProvider(),
            unit: "{entry}",
            description: "Current number of entries in WorkflowCache");
    }

    // ---------------------------------------------------------------------------------
    // Static helper methods called from RulesEngineService
    // ---------------------------------------------------------------------------------

    /// <summary>Records a cache hit for the given tenant.</summary>
    public static void RecordHit(string tenantId) =>
        HitCounter.Add(1, new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Records a cache miss for the given tenant.</summary>
    public static void RecordMiss(string tenantId) =>
        MissCounter.Add(1, new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>Records that <paramref name="count"/> entries were evicted.</summary>
    public static void RecordEviction(int count) =>
        EvictionCounter.Add(count);

    /// <summary>Records the hot-reload propagation lag in milliseconds.</summary>
    public static void RecordHotReloadLag(double milliseconds) =>
        HotReloadLagHistogram.Record(milliseconds);
}
