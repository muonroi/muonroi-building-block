namespace Muonroi.RuleEngine.Runtime.Tests.Rules;

/// <summary>
/// Unit tests for WorkflowCacheTelemetry OTel metrics instrumentation.
/// Uses MeterListener subscribed BEFORE the action to capture metric recordings.
/// Uses reflection to access private static members (Phase31 pattern).
/// </summary>
[Trait("Category", "Phase38")]
public class WorkflowCacheTelemetryTests : IDisposable
{
    // Reflection access to private static WorkflowCache field
    private static readonly FieldInfo CacheField = typeof(RulesEngineService)
        .GetField("WorkflowCache", BindingFlags.NonPublic | BindingFlags.Static)!;

    // Reflection access to private static GetOrCreateWorkflowDefinition method
    private static readonly MethodInfo GetOrCreateMethod = typeof(RulesEngineService)
        .GetMethod("GetOrCreateWorkflowDefinition", BindingFlags.NonPublic | BindingFlags.Static)!;

    private const string ValidJson = """[{"WorkflowName":"wf","Rules":["R1"]}]""";

    private static void ClearCache()
    {
        object cache = CacheField.GetValue(null)!;
        cache.GetType().GetMethod("Clear")!.Invoke(cache, null);
    }

    private static int GetCacheCount()
    {
        object cache = CacheField.GetValue(null)!;
        return (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
    }

    public WorkflowCacheTelemetryTests()
    {
        ClearCache();
    }

    public void Dispose()
    {
        ClearCache();
    }

    /// <summary>
    /// Test 1: GetOrCreateWorkflowDefinition cache hit increments HitCounter by 1.
    /// Listener is subscribed before the action to capture the measurement.
    /// </summary>
    [Fact]
    public void GetOrCreate_CacheHit_IncrementsHitCounter()
    {
        // Arrange — prime the cache with a first call (miss)
        GetOrCreateMethod.Invoke(null, ["tenant1", "wf1", ValidJson]);

        // Set up listener BEFORE the action
        long[] hits = [0L];
        using MeterListener listener = CreateLongCounterListener(
            WorkflowCacheTelemetry.MeterName,
            "muonroi.ruleengine.workflowcache.hits",
            hits);

        // Act — second call with same JSON = cache hit
        GetOrCreateMethod.Invoke(null, ["tenant1", "wf1", ValidJson]);

        // Assert
        hits[0].Should().Be(1, "one cache hit should increment HitCounter by 1");
    }

    /// <summary>
    /// Test 2: GetOrCreateWorkflowDefinition cache miss increments MissCounter by 1.
    /// </summary>
    [Fact]
    public void GetOrCreate_CacheMiss_IncrementsMissCounter()
    {
        // Set up listener BEFORE the action
        long[] misses = [0L];
        using MeterListener listener = CreateLongCounterListener(
            WorkflowCacheTelemetry.MeterName,
            "muonroi.ruleengine.workflowcache.misses",
            misses);

        // Act — first call for this key = cache miss
        GetOrCreateMethod.Invoke(null, ["tenant_miss", "wf_miss", ValidJson]);

        // Assert
        misses[0].Should().Be(1, "one cache miss should increment MissCounter by 1");
    }

    /// <summary>
    /// Test 3: EvictOldestEntries called when cache exceeds max → EvictionCounter incremented.
    /// Fills cache beyond MaxWorkflowCacheEntries to trigger eviction via GetOrCreate.
    /// </summary>
    [Fact]
    public void EvictOldestEntries_IncrementsEvictionCounterByEvictedCount()
    {
        // Arrange — get MaxWorkflowCacheEntries constant via reflection
        int maxEntries = (int)typeof(RulesEngineService)
            .GetField("MaxWorkflowCacheEntries", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        ClearCache();

        // Set up listener BEFORE filling cache past the limit
        long[] evicted = [0L];
        using MeterListener listener = CreateLongCounterListener(
            WorkflowCacheTelemetry.MeterName,
            "muonroi.ruleengine.workflowcache.evictions",
            evicted);

        // Act — add maxEntries + 1 entries to trigger LRU eviction
        for (int i = 0; i <= maxEntries; i++)
        {
            string json = $$"""[{"WorkflowName":"wf{{i}}","Rules":["R1"]}]""";
            GetOrCreateMethod.Invoke(null, ["t1", $"wf{i}", json]);
        }

        // Assert — eviction should have fired with MaxWorkflowCacheEntries/4 = 512 entries evicted
        evicted[0].Should().Be(maxEntries / 4,
            $"LRU eviction removes 25% ({maxEntries / 4}) of {maxEntries} entries");
    }

    /// <summary>
    /// Test 4: CacheSizeGauge observable returns WorkflowCache.Count via registered provider.
    /// </summary>
    [Fact]
    public void CacheSizeGauge_ReturnsCurrentCacheCount()
    {
        // Arrange — add a known number of entries
        ClearCache();
        GetOrCreateMethod.Invoke(null, ["tenant_gauge", "wf_a", ValidJson]);
        string json2 = """[{"WorkflowName":"wf_b","Rules":["R2"]}]""";
        GetOrCreateMethod.Invoke(null, ["tenant_gauge", "wf_b", json2]);

        int expectedCount = GetCacheCount();

        // Act — read gauge value via MeterListener (observable gauges are polled via RecordObservableInstruments)
        int gaugeValue = ReadCurrentGaugeValue(
            WorkflowCacheTelemetry.MeterName,
            "muonroi.ruleengine.workflowcache.size");

        // Assert
        gaugeValue.Should().Be(expectedCount,
            $"CacheSizeGauge should return current WorkflowCache.Count = {expectedCount}");
    }

    /// <summary>
    /// Test 5: NotifyRuleChangedAsync records hot-reload lag histogram — at least one recording >= 0ms.
    /// </summary>
    [Fact]
    public async Task NotifyRuleChangedAsync_RecordsHotReloadLagHistogram()
    {
        // Arrange — workflow name in JSON must match the workflowName parameter
        const string workflowName = "wf_reload";
        string reloadJson = $$"""[{"WorkflowName":"{{workflowName}}","Rules":["R1"]}]""";

        InMemoryRuleSetStore store = new();
        await store.SaveAsync(workflowName, reloadJson);

        RulesEngineService svc = new(store);

        int[] recordingCount = [0];
        double[] recordedMs = [-1.0];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WorkflowCacheTelemetry.MeterName &&
                instrument.Name == "muonroi.ruleengine.workflowcache.hotreload_lag_ms")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (instrument.Meter.Name == WorkflowCacheTelemetry.MeterName &&
                instrument.Name == "muonroi.ruleengine.workflowcache.hotreload_lag_ms")
            {
                Interlocked.Increment(ref recordingCount[0]);
                recordedMs[0] = measurement;
            }
        });
        listener.Start();

        // Act — SaveRuleSetAsync internally calls NotifyRuleChangedAsync which records the histogram
        await svc.SaveRuleSetAsync(workflowName, reloadJson);

        // Assert — at least one histogram recording was made, and the value is >= 0
        recordingCount[0].Should().BeGreaterThan(0,
            "NotifyRuleChangedAsync must record hot-reload lag at least once");
        recordedMs[0].Should().BeGreaterThanOrEqualTo(0,
            "hot-reload lag must be a non-negative duration in milliseconds");
    }

    // ---------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Creates a MeterListener subscribed to a long counter instrument.
    /// Measurements are accumulated via Interlocked into <paramref name="accumulator"/>[0].
    /// Caller must dispose the listener after the action under test.
    /// </summary>
    private static MeterListener CreateLongCounterListener(
        string meterName,
        string instrumentName,
        long[] accumulator)
    {
        MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
            {
                Interlocked.Add(ref accumulator[0], measurement);
            }
        });
        listener.Start();
        return listener;
    }

    private static int ReadCurrentGaugeValue(string meterName, string instrumentName)
    {
        int[] value = [0];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((instrument, measurement, _, _) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
            {
                value[0] = measurement;
            }
        });
        listener.Start();
        // Force the observable gauge to report its current value
        listener.RecordObservableInstruments();
        return value[0];
    }
}

/// <summary>
/// Minimal in-memory IRuleSetStore for telemetry tests.
/// </summary>
internal sealed class InMemoryRuleSetStore : IRuleSetStore
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(string workflowName, string json, CancellationToken ct = default)
    {
        _store[workflowName] = json;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string workflowName, int? version = null, CancellationToken ct = default)
    {
        _store.TryGetValue(workflowName, out string? json);
        return Task.FromResult(json);
    }

    public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<int>());

    public Task SetActiveVersionAsync(string workflowName, int version, CancellationToken ct = default)
        => Task.CompletedTask;
}
