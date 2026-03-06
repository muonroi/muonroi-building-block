namespace Muonroi.BuildingBlock.Test;

public class RulesEngineServiceHotReloadTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldUseRuntimeCache_AndReloadAfterSave()
    {
        TenantContext.CurrentTenantId = "tenant-1";
        CountingRuleSetStore store = new();
        InMemoryRuleSetChangeNotifier notifier = new();
        RuleSetRuntimeCache cache = new(
            new MemoryCache(new MemoryCacheOptions()),
            new RuleStoreConfigs { EnableRuntimeCache = true, RuntimeCacheMinutes = 60 },
            notifier);

        RulesEngineService service = new(store, runtimeCache: cache, notifier: notifier);

        await service.SaveRuleSetAsync("wf", """
                                            [
                                              {
                                                "WorkflowName": "wf",
                                                "Rules": [ "HotReloadRule1" ]
                                              }
                                            ]
                                            """);

        _ = await service.ExecuteAsync("wf", new HotReloadContext());
        _ = await service.ExecuteAsync("wf", new HotReloadContext());
        Assert.Equal(1, store.GetCallCount);

        await service.SaveRuleSetAsync("wf", """
                                            [
                                              {
                                                "WorkflowName": "wf",
                                                "Rules": [ "HotReloadRule2" ]
                                              }
                                            ]
                                            """);

        _ = await service.ExecuteAsync("wf", new HotReloadContext());
        Assert.Equal(2, store.GetCallCount);
        TenantContext.CurrentTenantId = null;
    }

    private sealed class HotReloadContext;

    private sealed class HotReloadRule1 : IRule<HotReloadContext>
    {
        public string Code => "HotReloadRule1";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(HotReloadContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(HotReloadContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class HotReloadRule2 : IRule<HotReloadContext>
    {
        public string Code => "HotReloadRule2";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(HotReloadContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(HotReloadContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CountingRuleSetStore : IRuleSetStore
    {
        private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
        public int GetCallCount { get; private set; }

        public Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default)
        {
            _map[workflowName] = json;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string workflowName, int? version = null, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            _map.TryGetValue(workflowName, out string? json);
            return Task.FromResult(json);
        }

        public Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Array.Empty<int>());
        }
    }
}
