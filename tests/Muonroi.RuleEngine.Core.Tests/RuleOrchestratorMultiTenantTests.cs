using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core.Tests;

public class RuleOrchestratorMultiTenantTests
{
    [TenantRuleGroup("workflow", "tenantA")]
    private sealed class TenantScopedRule : IRule<string>
    {
        public string Name => "TenantScopedRule";
        public string Code => "TenantScopedRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task<RuleResult> EvaluateAsync(string context, FactBag facts, CancellationToken ct)
        {
            facts["tenant"] = "tenantA";
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ResolveKeyedOrchestrator_ForTenant()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddRuleEngine();
        services.AddScoped<TenantScopedRule>();
        services.AddKeyedScoped<IRule<string>, TenantScopedRule>("workflow:tenantA");
        services.AddKeyedScoped("workflow:tenantA", (sp, _) =>
            new RuleOrchestrator<string>(
                sp.GetRequiredKeyedService<IEnumerable<IRule<string>>>("workflow:tenantA"),
                sp.GetKeyedService<IEnumerable<IHookHandler<string>>>("workflow:tenantA") ?? [],
                sp.GetService<IMLog<RuleOrchestrator<string>>>(),
                sp.GetKeyedService<IEnumerable<IRuleEventListener<string>>>("workflow:tenantA") ?? []));

        IServiceProvider provider = services.BuildServiceProvider();
        RuleOrchestrator<string> orchestrator = provider.GetRequiredKeyedService<RuleOrchestrator<string>>("workflow:tenantA");

        FactBag facts = await orchestrator.ExecuteAsync("ctx");
        Assert.Equal("tenantA", facts["tenant"]);
    }
}