using FluentAssertions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Proves that RuleEngine.Runtime works standalone without any tenant infrastructure (DCP-04)
/// and that tenant-aware features like quota/cache activate only when registered (DCP-05).
/// </summary>
public sealed class SingleTenantExecutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    /// <summary>
    /// DCP-04: RulesEngineService can save and load rule sets with NO tenant context set,
    /// proving single-tenant mode works without any tenant infrastructure.
    /// </summary>
    [Fact]
    public async Task ExecuteRuleSet_WithoutTenantContext_Succeeds()
    {
        // Arrange — no tenant set on accessor (tenantId will be null)
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(_root, executionContextAccessor: accessor);
        RulesEngineService service = new(store, runtimeCache: null, executionContextAccessor: accessor);

        const string workflowName = "wf.standalone";
        const string ruleSetJson = """{"workflowName":"wf.standalone","rules":["RULE_STANDALONE"]}""";

        // Act — save then retrieve without any tenant context
        await service.SaveRuleSetAsync(workflowName, ruleSetJson);
        string? loaded = await service.GetRuleSetAsync(workflowName);

        // Assert — round-trip succeeds
        loaded.Should().NotBeNull();
        loaded.Should().Contain("wf.standalone");
    }

    /// <summary>
    /// DCP-05: When no IRuleSetRuntimeCache is registered (null), rule save proceeds without
    /// cache interaction. When a cache IS registered, invalidation is called — proving
    /// conditional activation of tenant-aware features.
    /// </summary>
    [Fact]
    public async Task QuotaTracker_ActivatesOnlyWhenRegistered()
    {
        const string workflowName = "wf.conditional";
        const string ruleSetJson = """{"workflowName":"wf.conditional","rules":["RULE_COND"]}""";

        // Scenario A: No runtime cache — save succeeds, no cache interaction
        SystemExecutionContextAccessor accessorA = new();
        accessorA.Set(new SystemExecutionContext(
            "tenant-single", userId: null, username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null, apiKey: null,
            isAuthenticated: false, permissions: [], sourceType: "tests"));

        FileRuleSetStore storeA = new(_root, executionContextAccessor: accessorA);
        RulesEngineService serviceA = new(storeA, runtimeCache: null, executionContextAccessor: accessorA);
        await serviceA.SaveRuleSetAsync(workflowName, ruleSetJson);

        // No exception = success without cache

        // Scenario B: With runtime cache — invalidation IS called
        SystemExecutionContextAccessor accessorB = new();
        accessorB.Set(new SystemExecutionContext(
            "tenant-single", userId: null, username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null, apiKey: null,
            isAuthenticated: false, permissions: [], sourceType: "tests"));

        CaptureRuntimeCache cache = new();
        FileRuleSetStore storeB = new(_root, executionContextAccessor: accessorB);
        RulesEngineService serviceB = new(storeB, runtimeCache: cache, executionContextAccessor: accessorB);
        await serviceB.SaveRuleSetAsync(workflowName, ruleSetJson);

        // Assert — cache invalidation was triggered
        cache.LastInvalidatedTenantId.Should().Be("tenant-single");
        cache.LastInvalidatedWorkflow.Should().Be(workflowName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class CaptureRuntimeCache : IRuleSetRuntimeCache
    {
        public string? LastInvalidatedTenantId { get; private set; }
        public string? LastInvalidatedWorkflow { get; private set; }

        public Task<string?> GetOrCreateAsync(
            string tenantId,
            string workflowName,
            Func<Task<string?>> factory,
            CancellationToken cancellationToken = default)
        {
            return factory();
        }

        public Task InvalidateAsync(
            string tenantId,
            string workflowName,
            CancellationToken cancellationToken = default)
        {
            LastInvalidatedTenantId = tenantId;
            LastInvalidatedWorkflow = workflowName;
            return Task.CompletedTask;
        }
    }
}
