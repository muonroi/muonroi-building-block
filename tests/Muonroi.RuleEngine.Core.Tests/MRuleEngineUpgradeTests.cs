using Muonroi.RuleEngine.Testing;

namespace Muonroi.RuleEngine.Core.Tests;

public class MRuleEngineUpgradeTests
{
    private sealed class PersistRule : IRule<string>
    {
        public string Code => "PERSIST";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => "PersistRule";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(string ctx, FactBag facts, CancellationToken ct)
        {
            facts["persist"] = true;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AfterRule : IRule<string>
    {
        public string Code => "AFTER";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.AfterPersist;
        public RuleType Type => RuleType.Validation;
        public string Name => "AfterRule";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(string ctx, FactBag facts, CancellationToken ct)
        {
            facts["after"] = true;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AddRuleEngine_GenericBuilder_RegistersRulesAndRouter()
    {
        ServiceCollection services = new();
        services.AddRuleEngine<string>(o => o.ExecutionMode = RuleExecutionMode.Rules)
            .AddRule<PersistRule>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMRuleExecutionRouter<string> router = provider.GetRequiredService<IMRuleExecutionRouter<string>>();

        FactBag facts = await router.ExecuteAsync("ctx");

        Assert.True(facts.Get<bool>("persist"));
    }

    [Fact]
    public async Task Router_TraditionalMode_UsesTraditionalDelegate()
    {
        ServiceCollection services = new();
        services.AddRuleEngine<string>(o => o.ExecutionMode = RuleExecutionMode.Traditional)
            .AddRule<PersistRule>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMRuleExecutionRouter<string> router = provider.GetRequiredService<IMRuleExecutionRouter<string>>();
        bool called = false;

        FactBag facts = await router.ExecuteAsync("ctx", _ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        Assert.True(called);
        Assert.Empty(facts.AsReadOnly());
    }

    [Fact]
    public async Task RuleOrchestrator_FilterPoint_ExecutesOnlyMatchingRule()
    {
        RuleOrchestrator<string> orchestrator = new([new PersistRule(), new AfterRule()], [], null);

        FactBag facts = await orchestrator.ExecuteAsync("ctx", HookPoint.BeforePersist);

        Assert.True(facts.Get<bool>("persist"));
        Assert.False(facts.AsReadOnly().ContainsKey("after"));
    }

    [Fact]
    public async Task MRuleTestBuilder_ForRule_SeedsFactsAndReturnsResult()
    {
        MRuleTestResult result = await Muonroi.RuleEngine.Testing.MRuleTestBuilder<TestContext>
            .ForRule<TestRule>()
            .WithFact("seed", 42)
            .WithContext(ctx => ctx.Value = 5)
            .ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Facts.Get<int>("seed"));
        result.Facts.Should().Contain("value", 5).NotContain("missing");
    }

    private sealed class TestContext
    {
        public int Value { get; set; }
    }

    private sealed class TestRule : IRule<TestContext>
    {
        public string Code => "TEST";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public string Name => "TestRule";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(TestContext ctx, FactBag facts, CancellationToken ct)
        {
            facts["value"] = ctx.Value;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
