namespace Muonroi.Rules.Tests.Rules;

public class RuleEngineGenericTests
{
    private sealed class TestContext
    {
        public int Value { get; set; }
    }

    private sealed class PassRule : IRule<TestContext>
    {
        public string Code => "pass-rule";
        public string Name => "PassRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(TestContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FailRule : IRule<TestContext>
    {
        public string Code => "fail-rule";
        public string Name => "FailRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.AfterRule;
        public RuleType Type => RuleType.Business;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(TestContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(new RuleResult(false, ["Failed"]));

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Rule failed");
    }

    [Fact]
    public void AddRule_WithDescriptor_ReturnsEngineForChaining()
    {
        var engine = new RuleEngine<TestContext>();
        var descriptor = new RuleDescriptor("test", "Test", "desc", RuleType.Validation);
        var result = engine.AddRule(new PassRule(), descriptor);
        result.Should().BeSameAs(engine);
    }

    [Fact]
    public void AddRule_WithoutDescriptor_ReturnsEngineForChaining()
    {
        var engine = new RuleEngine<TestContext>();
        var result = engine.AddRule(new PassRule());
        result.Should().BeSameAs(engine);
    }

    [Fact]
    public void GetCatalog_ReturnsRegisteredRules()
    {
        var engine = new RuleEngine<TestContext>();
        engine.AddRule(new PassRule());
        var catalog = engine.GetCatalog().ToList();
        catalog.Should().HaveCount(1);
        catalog[0].Code.Should().Be("pass-rule");
    }

    [Fact]
    public void RemoveRule_ExistingCode_ReturnsTrue()
    {
        var engine = new RuleEngine<TestContext>();
        engine.AddRule(new PassRule());
        bool removed = engine.RemoveRule("pass-rule");
        removed.Should().BeTrue();
        engine.GetCatalog().Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_NonExistingCode_ReturnsFalse()
    {
        var engine = new RuleEngine<TestContext>();
        bool removed = engine.RemoveRule("nonexistent");
        removed.Should().BeFalse();
    }

    [Fact]
    public void RemoveRule_EmptyCode_ReturnsFalse()
    {
        var engine = new RuleEngine<TestContext>();
        bool removed = engine.RemoveRule("");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PassingRule_CompletesSuccessfully()
    {
        var engine = new RuleEngine<TestContext>();
        engine.AddRule(new PassRule());
        await engine.ExecuteAsync(new TestContext { Value = 1 }, RuleType.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_FailingRule_ThrowsException()
    {
        var engine = new RuleEngine<TestContext>();
        engine.AddRule(new FailRule());
        Func<Task> act = () => engine.ExecuteAsync(new TestContext(), RuleType.Business);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_FiltersByRuleType()
    {
        var engine = new RuleEngine<TestContext>();
        engine.AddRule(new PassRule());
        engine.AddRule(new FailRule());
        // Should only run Validation rules, not Business - so no exception
        await engine.ExecuteAsync(new TestContext(), RuleType.Validation);
    }
}
