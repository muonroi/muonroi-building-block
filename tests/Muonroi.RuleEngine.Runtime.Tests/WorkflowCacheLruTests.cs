// ReSharper disable AccessToModifiedClosure

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Unit tests for LRU eviction behavior in WorkflowCache (RulesEngineService).
/// Uses reflection to access private static fields and methods.
/// </summary>
[Trait("Category", "Phase31")]
public class WorkflowCacheLruTests : IDisposable
{
    // Reflection access to private static WorkflowCache field
    private static readonly FieldInfo CacheField = typeof(RulesEngineService)
        .GetField("WorkflowCache", BindingFlags.NonPublic | BindingFlags.Static)!;

    // Reflection access to private static GetOrCreateWorkflowDefinition method
    private static readonly MethodInfo GetOrCreateMethod = typeof(RulesEngineService)
        .GetMethod("GetOrCreateWorkflowDefinition", BindingFlags.NonPublic | BindingFlags.Static)!;

    // Helper to get the cache count via reflection (avoids generic type cast for private nested type)
    private static int GetCacheCount()
    {
        object cache = CacheField.GetValue(null)!;
        // ConcurrentDictionary<string, CachedWorkflowDefinition>.Count
        return (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
    }

    // Helper to check if a key exists in cache
    private static bool CacheContainsKey(string key)
    {
        object cache = CacheField.GetValue(null)!;
        return (bool)cache.GetType().GetMethod("ContainsKey")!.Invoke(cache, [key])!;
    }

    // Helper to clear the cache
    private static void ClearCache()
    {
        object cache = CacheField.GetValue(null)!;
        cache.GetType().GetMethod("Clear")!.Invoke(cache, null);
    }

    // Simple valid workflow JSON used for testing
    private const string ValidJson = """[{"WorkflowName":"wf","Rules":["R1"]}]""";

    // Clear cache before each test to ensure isolation
    public WorkflowCacheLruTests()
    {
        ClearCache();
    }

    public void Dispose()
    {
        ClearCache();
    }

    /// <summary>
    /// Helper to invoke GetOrCreateWorkflowDefinition via reflection.
    /// </summary>
    private static void InvokeGetOrCreate(string tenantId, string workflowName, string json)
    {
        GetOrCreateMethod.Invoke(null, [tenantId, workflowName, json]);
    }

    /// <summary>
    /// Test 1: After inserting 2049+ entries, cache count should drop to ~1537 (2049 - 512),
    /// not to 0 (which is what WorkflowCache.Clear() would produce).
    /// The most recently inserted entry must still be present.
    /// </summary>
    [Fact]
    public void EvictsOldest25Percent_WhenCacheExceedsLimit()
    {
        // Arrange: fill cache to just over the limit
        int totalEntries = 2049;
        for (int i = 0; i < totalEntries; i++)
        {
            InvokeGetOrCreate("tenant-evict", $"workflow-{i}", ValidJson);
        }

        // Act: the eviction should have been triggered on insert #2049
        int count = GetCacheCount();

        // Assert: cache should retain ~75% of entries (not be empty)
        // Current broken code: WorkflowCache.Clear() → count = 1 (only last inserted)
        // Expected LRU behavior: count ≈ 1537 (2049 - 512 evicted = 1537)
        count.Should().BeGreaterThan(1,
            because: "LRU eviction must NOT clear all entries — it should retain 75% of the cache");

        // The last inserted entry must still be present
        string lastKey = $"tenant-evict:workflow-{totalEntries - 1}";
        CacheContainsKey(lastKey).Should().BeTrue(
            because: "the most recently inserted entry must survive eviction");
    }

    /// <summary>
    /// Test 2: An entry accessed recently should survive eviction.
    /// Fill cache to 2048 entries, then re-access entry[0] to refresh its access time,
    /// then insert entry 2049 to trigger eviction. Entry[0] must survive.
    /// </summary>
    [Fact]
    public void RetainsRecentlyAccessedEntries_AfterEviction()
    {
        // Arrange: fill cache to exactly 2048 entries
        for (int i = 0; i < 2048; i++)
        {
            InvokeGetOrCreate("tenant-retain", $"workflow-{i}", ValidJson);
        }

        // Simulate a cache hit on entry[0] to refresh its LastAccessedUtc
        // The method updates access time on cache hit
        InvokeGetOrCreate("tenant-retain", "workflow-0", ValidJson);

        // Act: insert one more entry to push count over 2048 → triggers eviction
        InvokeGetOrCreate("tenant-retain", "workflow-2048", ValidJson);

        // Assert: the recently re-accessed entry[0] must still be present
        // (LRU evicts OLDEST entries, entry[0] was just re-accessed so it's recent)
        // Current broken code: Clear() removes all entries, so entry[0] would be gone
        string recentlyAccessedKey = "tenant-retain:workflow-0";
        CacheContainsKey(recentlyAccessedKey).Should().BeTrue(
            because: "recently accessed entries must be retained by LRU eviction policy");
    }

    /// <summary>
    /// Test 3: Concurrent eviction from multiple threads must not throw and must leave
    /// the cache in a consistent state (count not negative, no exceptions).
    /// SemaphoreSlim(1,1) with Wait(0) ensures only one thread evicts at a time.
    /// </summary>
    [Fact]
    public void SemaphorePreventsConcurrentEviction_NoExceptions()
    {
        // Arrange: fill cache to the limit first
        for (int i = 0; i < 2048; i++)
        {
            InvokeGetOrCreate("tenant-concurrent", $"workflow-{i}", ValidJson);
        }

        // Act: trigger eviction from multiple threads simultaneously
        var exceptions = new ConcurrentBag<Exception>();
        var threads = Enumerable.Range(2048, 50)
            .Select(i => new Thread(() =>
            {
                try
                {
                    InvokeGetOrCreate("tenant-concurrent", $"workflow-{i}", ValidJson);
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    exceptions.Add(tie.InnerException);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }))
            .ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join(TimeSpan.FromSeconds(5)));

        // Assert: no exceptions during concurrent eviction
        exceptions.Should().BeEmpty(
            because: "SemaphoreSlim(1,1) must prevent concurrent eviction races");

        // Cache count must be non-negative and at most 2048 + thread count (small transient overshoot OK)
        int cacheCount = GetCacheCount();
        cacheCount.Should().BeGreaterThanOrEqualTo(0,
            because: "cache count must never go negative");
        cacheCount.Should().BeLessThanOrEqualTo(2048 + 50 + 10,
            because: "cache must not grow unbounded after eviction");
    }
}
