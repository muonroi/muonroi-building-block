using Muonroi.RuleEngine.Core.Workflow;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Core.Tests;

public class RuleWorkflowRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_RoutesThroughGateway_AndCollectsFacts()
    {
        ServiceCollection services = new();
        services
            .AddRuleEngine<int>(o => o.ExecutionMode = RuleExecutionMode.Rules)
            .AddRule<ParityRule>()
            .AddRule<SquareRule>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMRuleWorkflowRunner<int> runner = provider.GetRequiredService<IMRuleWorkflowRunner<int>>();

        MRuleWorkflowDefinition<int> workflow = new(
            "number-check",
            "start",
            [
                MRuleWorkflowStep<int>.Start("start", "evaluate"),
                MRuleWorkflowStep<int>.RuleTask("evaluate", "route"),
                MRuleWorkflowStep<int>.ExclusiveGateway("route", (ctx, _) =>
                {
                    string next = ctx.Facts.Get<bool>("even") ? "approved" : "rejected";
                    return Task.FromResult(next);
                }),
                MRuleWorkflowStep<int>.ServiceTask("approved", "end", (ctx, _) =>
                {
                    ctx.Facts["decision"] = "approved";
                    return Task.CompletedTask;
                }),
                MRuleWorkflowStep<int>.ServiceTask("rejected", "end", (ctx, _) =>
                {
                    ctx.Facts["decision"] = "rejected";
                    return Task.CompletedTask;
                }),
                MRuleWorkflowStep<int>.End("end")
            ]);

        MRuleWorkflowResult<int> result = await runner.ExecuteAsync(8, workflow);

        Assert.Equal("approved", result.Facts.Get<string>("decision"));
        Assert.Equal(64, result.Facts.Get<int>("square"));
        Assert.Equal(["start", "evaluate", "route", "approved", "end"], result.ExecutedSteps);
    }

    [Fact]
    public async Task ExecuteAsync_TraditionalRuleTask_UsesTraditionalPath()
    {
        ServiceCollection services = new();
        services
            .AddRuleEngine<int>(o => o.ExecutionMode = RuleExecutionMode.Rules)
            .AddRule<ParityRule>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMRuleWorkflowRunner<int> runner = provider.GetRequiredService<IMRuleWorkflowRunner<int>>();

        MRuleWorkflowDefinition<int> workflow = new(
            "traditional-path",
            "start",
            [
                MRuleWorkflowStep<int>.Start("start", "evaluate"),
                MRuleWorkflowStep<int>.RuleTask(
                    "evaluate",
                    "end",
                    RuleExecutionMode.Traditional,
                    (ctx, _) =>
                    {
                        ctx.SetState("path", "traditional");
                        ctx.Facts["decision"] = "traditional";
                        return Task.CompletedTask;
                    }),
                MRuleWorkflowStep<int>.End("end")
            ]);

        MRuleWorkflowResult<int> result = await runner.ExecuteAsync(3, workflow);

        Assert.Equal("traditional", result.State["path"]);
        Assert.Equal("traditional", result.Facts.Get<string>("decision"));
        Assert.Equal(["start", "evaluate", "end"], result.ExecutedSteps);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowCyclesBeyondLimit_Throws()
    {
        ServiceCollection services = new();
        services.AddRuleEngine<int>(o => o.ExecutionMode = RuleExecutionMode.Rules);
        services.ConfigureRuleWorkflow(o => o.MaxSteps = 3);

        using ServiceProvider provider = services.BuildServiceProvider();
        IMRuleWorkflowRunner<int> runner = provider.GetRequiredService<IMRuleWorkflowRunner<int>>();

        MRuleWorkflowDefinition<int> workflow = new(
            "loop",
            "start",
            [
                MRuleWorkflowStep<int>.Start("start", "loop"),
                MRuleWorkflowStep<int>.ServiceTask("loop", "loop", (_, _) => Task.CompletedTask),
                MRuleWorkflowStep<int>.End("end")
            ]);

        await Assert.ThrowsAsync<MInternalException>(() => runner.ExecuteAsync(1, workflow));
    }

    private sealed class ParityRule : IRule<int>
    {
        public string Code => "PARITY";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public string Name => "ParityRule";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(int ctx, FactBag facts, CancellationToken ct)
        {
            facts["even"] = ctx % 2 == 0;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(int context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SquareRule : IRule<int>
    {
        public string Code => "SQUARE";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["PARITY"];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Business;
        public string Name => "SquareRule";
        public IEnumerable<Type> Dependencies => [typeof(ParityRule)];

        public Task<RuleResult> EvaluateAsync(int ctx, FactBag facts, CancellationToken ct)
        {
            facts["square"] = ctx * ctx;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(int context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
