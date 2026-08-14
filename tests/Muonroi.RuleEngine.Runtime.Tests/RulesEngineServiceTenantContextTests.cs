namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RulesEngineServiceTenantContextTests
{
    [Fact]
    public async Task SaveRuleSetAsync_ShouldInvalidateRuntimeCacheForCurrentExecutionTenant()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(root, executionContextAccessor: accessor);
        CaptureRuntimeCache cache = new();
        RulesEngineService service = new(store, runtimeCache: cache, executionContextAccessor: accessor);

        SetTenant(accessor, "tenant-alpha");
        await service.SaveRuleSetAsync("wf.orders", "{\"workflowName\":\"wf.orders\",\"rules\":[\"RULE_A\"]}");

        cache.LastInvalidatedTenantId.Should().Be("tenant-alpha");
        cache.LastInvalidatedWorkflow.Should().Be("wf.orders");
    }

    private static void SetTenant(ISystemExecutionContextAccessor accessor, string tenantId)
    {
        accessor.Set(new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "tests"));
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
