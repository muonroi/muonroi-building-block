namespace Muonroi.Rules.Tests.Rules;

public sealed class RulesEngineServiceCodeWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WithMissingRuleCode_Throws()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync("wf", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns("""{"WorkflowName":"wf","Rules":["missing-code"]}""");

        RulesEngineService service = new(store, serviceProvider: new ServiceCollection().BuildServiceProvider());

        Func<Task> act = () => service.ExecuteAsync("wf", new CodeWorkflowContext());

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*no rule implementations were discovered*");
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WithAmbiguousRuleMappings_Throws()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync("wf", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns("""{"WorkflowName":"wf","Rules":["duplicate-code"]}""");

        ServiceCollection services = new();
        services.AddSingleton<IRule<CodeWorkflowContext>, DuplicateRuleA>();
        services.AddSingleton<IRule<CodeWorkflowContext>, DuplicateRuleB>();
        RulesEngineService service = new(store, serviceProvider: services.BuildServiceProvider());

        Func<Task> act = () => service.ExecuteAsync("wf", new CodeWorkflowContext());

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*ambiguous rule code mappings*");
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WithResolvedRule_ReturnsResultFact()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync("wf", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns("""{"WorkflowName":"wf","Rules":["set-result"]}""");

        ServiceCollection services = new();
        services.AddSingleton<IRule<CodeWorkflowContext>, SetResultRule>();
        RulesEngineService service = new(store, serviceProvider: services.BuildServiceProvider());
        CodeWorkflowContext context = new();

        FactBag result = await service.ExecuteAsync("wf", context);

        context.Result.Should().Be("done");
        result["Result"].Should().Be("done");
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WithKnownAndUnknownRuleCodes_ThrowsUnknownRuleCodes()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync("wf", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns("""{"WorkflowName":"wf","Rules":["set-result","missing-code"]}""");

        ServiceCollection services = new();
        services.AddSingleton<IRule<CodeWorkflowContext>, SetResultRule>();
        RulesEngineService service = new(store, serviceProvider: services.BuildServiceProvider());

        Func<Task> act = () => service.ExecuteAsync("wf", new CodeWorkflowContext());

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*unknown rule code(s): missing-code*");
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WithReflectionDiscoveredPrivateRule_ReturnsResultFact()
    {
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        store.GetAsync("wf", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns("""{"WorkflowName":"wf","Rules":["private-set-result"]}""");

        RulesEngineService service = new(store, serviceProvider: new ServiceCollection().BuildServiceProvider());
        ReflectionWorkflowContext context = new();

        FactBag result = await service.ExecuteAsync("wf", context);

        context.Result.Should().Be("reflected");
        result["Result"].Should().Be("reflected");
    }

    private sealed class CodeWorkflowContext
    {
        public string? Result { get; set; }
    }

    private sealed class ReflectionWorkflowContext
    {
        public string? Result { get; set; }
    }

    private sealed class SetResultRule : IRule<CodeWorkflowContext>
    {
        public string Code => "set-result";
        public string Name => "SetResultRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(CodeWorkflowContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(CodeWorkflowContext context, CancellationToken cancellationToken = default)
        {
            context.Result = "done";
            return Task.CompletedTask;
        }
    }

    private sealed class ReflectionOnlyRule : IRule<ReflectionWorkflowContext>
    {
        private ReflectionOnlyRule()
        {
        }

        public string Code => "private-set-result";
        public string Name => "ReflectionOnlyRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(ReflectionWorkflowContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(ReflectionWorkflowContext context, CancellationToken cancellationToken = default)
        {
            context.Result = "reflected";
            return Task.CompletedTask;
        }
    }

    private sealed class DuplicateRuleA : IRule<CodeWorkflowContext>
    {
        public string Code => "duplicate-code";
        public string Name => "DuplicateRuleA";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(CodeWorkflowContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(CodeWorkflowContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class DuplicateRuleB : IRule<CodeWorkflowContext>
    {
        public string Code => "duplicate-code";
        public string Name => "DuplicateRuleB";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(CodeWorkflowContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(CodeWorkflowContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
