using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class RuleOrchestratorTests
{
    private sealed class RuleA
    {
    }

    private sealed class RuleB
    {
    }

    private sealed class TestRule<TMarker>(
        string code,
        HookPoint hookPoint,
        int order = 0,
        IEnumerable<Type>? dependencies = null,
        Func<FactBag, Task<RuleResult>>? action = null
    ) : IRule<object>
    {
        private readonly Func<FactBag, Task<RuleResult>> _action =
            action ?? (_ => Task.FromResult(RuleResult.Success()));

        public string Code { get; } = code;
        public int Order { get; } = order;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint { get; } = hookPoint;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies { get; } = dependencies ?? [];

        public Task<RuleResult> EvaluateAsync(object ctx, FactBag facts, CancellationToken ct)
        {
            return _action(facts);
        }

        public Task ExecuteAsync(object context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static RuleOrchestrator<object> CreateOrchestrator(params IRule<object>[] rules)
    {
        IHookHandler<object>[] hooks = [];
        ILogger<RuleOrchestrator<object>> logger = new Mock<ILogger<RuleOrchestrator<object>>>().Object;
        return new RuleOrchestrator<object>(rules, hooks, logger);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesDependenciesInOrder()
    {
        List<string> executed = [];

        TestRule<RuleB> b = new("B", HookPoint.BeforePersist, action: facts =>
        {
            facts["B"] = 42;
            executed.Add("B");
            return Task.FromResult(RuleResult.Success());
        });

        TestRule<RuleA> a = new("A", HookPoint.BeforePersist, dependencies: [typeof(TestRule<RuleB>)], action: facts =>
        {
            if (!facts.TryGet("B", out int value) || value != 42)
                return Task.FromResult(RuleResult.Failure("missing fact"));
            executed.Add("A");
            return Task.FromResult(RuleResult.Success());
        });

        RuleOrchestrator<object> orchestrator = CreateOrchestrator(a, b);
        await orchestrator.ExecuteAsync(new object());

        Assert.Equal(new[] { "B", "A" }, executed);
    }

    [Fact]
    public void ExecuteAsync_DetectsCycle()
    {
        TestRule<RuleA> a = new("A", HookPoint.BeforePersist, dependencies: [typeof(TestRule<RuleB>)]);
        TestRule<RuleB> b = new("B", HookPoint.BeforePersist, dependencies: [typeof(TestRule<RuleA>)]);

        Assert.Throws<MInternalException>(() => CreateOrchestrator(a, b));
    }

    [Fact]
    public async Task ExecuteAsync_ShortCircuitsOnFailure()
    {
        List<string> executed = [];

        TestRule<RuleA> fail = new("A", HookPoint.BeforePersist, action: _ =>
        {
            executed.Add("A");
            return Task.FromResult(RuleResult.Failure("boom"));
        });

        TestRule<RuleB> later = new("B", HookPoint.BeforePersist, action: _ =>
        {
            executed.Add("B");
            return Task.FromResult(RuleResult.Success());
        });

        RuleOrchestrator<object> orchestrator = CreateOrchestrator(fail, later);

        await Assert.ThrowsAsync<MInternalException>(() => orchestrator.ExecuteAsync(new object()));

        Assert.Equal(new[] { "A" }, executed);
    }

    [Fact]
    public async Task ExecuteAsync_UsesOrderAsFallbackWhenNoDependenciesExist()
    {
        List<string> executed = [];

        TestRule<RuleA> second = new("SECOND", HookPoint.BeforePersist, order: 20, action: _ =>
        {
            executed.Add("SECOND");
            return Task.FromResult(RuleResult.Success());
        });

        TestRule<RuleB> first = new("FIRST", HookPoint.BeforePersist, order: 10, action: _ =>
        {
            executed.Add("FIRST");
            return Task.FromResult(RuleResult.Success());
        });

        RuleOrchestrator<object> orchestrator = CreateOrchestrator(second, first);
        await orchestrator.ExecuteAsync(new object());

        Assert.Equal(new[] { "FIRST", "SECOND" }, executed);
    }
}
