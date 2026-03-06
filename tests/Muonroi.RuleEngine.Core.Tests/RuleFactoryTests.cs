namespace Muonroi.RuleEngine.Core.Tests;

public class RuleFactoryTests
{
    private sealed class Dependency
    {
        public static int GetValue()
        {
            return 5;
        }
    }

    private sealed class DependentRule(Dependency dep) : IRule<string>
    {
        private readonly Dependency _dep = dep;
        public string Name => "DependentRule";
        public string Code => "DependentRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task<RuleResult> EvaluateAsync(string context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            facts["value"] = Dependency.GetValue();
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Factory_CreatesRuleWithDependencies()
    {
        ServiceCollection services = new();
        services.AddScoped<Dependency>();
        services.AddScoped<DependentRule>();
        services.AddRuleEngine();

        IServiceProvider provider = services.BuildServiceProvider();
        IRuleFactory<string> factory = provider.GetRequiredService<IRuleFactory<string>>();
        IRule<string> rule = factory.Create(typeof(DependentRule));

        FactBag bag = new();
        RuleResult result = await rule.EvaluateAsync("ctx", bag, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, bag["value"]);
    }
}