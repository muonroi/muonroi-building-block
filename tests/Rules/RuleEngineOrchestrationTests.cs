namespace Muonroi.Rules.Tests;

[Collection("NonParallel")]
public class RuleEngineOrchestrationTests
{
    private sealed class ThrowingRule : IRule<Context>
    {
        public RuleType Type => RuleType.Validation;
        public string Code => throw new NotImplementedException();
        public int Order => throw new NotImplementedException();
        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();
        public HookPoint HookPoint => throw new NotImplementedException();
        public string Name => throw new NotImplementedException();
        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task<RuleResult> EvaluateAsync(Context ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync(Context context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class TrackingRule(RuleType type) : IRule<Context>
    {
        public RuleType Type { get; } = type;
        public bool Executed { get; private set; }
        public string Code => throw new NotImplementedException();
        public int Order => throw new NotImplementedException();
        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();
        public HookPoint HookPoint => throw new NotImplementedException();
        public string Name => throw new NotImplementedException();
        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task ExecuteAsync(Context context, CancellationToken cancellationToken = default)
        {
            Executed = true;
            context.Value += 5;
            return Task.CompletedTask;
        }

        public Task<RuleResult> EvaluateAsync(Context ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class Context
    {
        public int Value { get; set; }
    }

    [Fact]
    public async Task ExecuteAsync_ShortCircuits_OnException()
    {
        Context context = new();
        TrackingRule goodRule = new(RuleType.Business);
        RuleEngine<Context> engine = new RuleEngine<Context>()
            .AddRule(new ThrowingRule())
            .AddRule(goodRule);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecuteAsync(context));
        Assert.False(goodRule.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_SharesContextBetweenRules()
    {
        Context context = new()
        {
            Value = 1
        };
        TrackingRule first = new(RuleType.Validation);
        TrackingRule second = new(RuleType.Business);
        RuleEngine<Context> engine = new RuleEngine<Context>()
            .AddRule(first)
            .AddRule(second);

        await engine.ExecuteAsync(context);

        Assert.True(first.Executed);
        Assert.True(second.Executed);
        Assert.Equal(11, context.Value);
    }
}
