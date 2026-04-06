using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleEngineTests
{
    private readonly ISystemExecutionContextAccessor _execCtx;

    public RuleEngineTests()
    {
        _execCtx = new SystemExecutionContextAccessor();
    }

    private RuleEngine<TestContext> CreateEngine(
        IOptionsMonitor<RuleOptions>? options = null,
        ILicenseGuard? licenseGuard = null)
    {
        return new RuleEngine<TestContext>(
            options: options,
            logger: null,
            activation: null,
            licenseGuard: licenseGuard,
            executionContextAccessor: _execCtx);
    }

    [Fact]
    public void AddRule_WithDescriptor_AddsToEngine()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        IRule<TestContext> rule = new FakeRule("rule-1");

        engine.AddRule(rule, new RuleDescriptor("rule-1", "Rule 1", "desc", RuleType.Validation));

        engine.GetCatalog().Should().ContainSingle(d => d.Code == "rule-1");
    }

    [Fact]
    public void AddRule_WithoutDescriptor_GeneratesDescriptor()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        IRule<TestContext> rule = new FakeRule("auto-code");

        engine.AddRule(rule);

        engine.GetCatalog().Should().ContainSingle(d => d.Code == "auto-code");
    }

    [Fact]
    public void AddRule_DuplicateCode_GeneratesUniqueCode()
    {
        RuleEngine<TestContext> engine = CreateEngine();

        engine.AddRule(new FakeRule("code-a"));
        engine.AddRule(new FakeRule("code-a"));

        IEnumerable<string> codes = engine.GetCatalog().Select(d => d.Code);
        codes.Should().Contain("code-a");
        codes.Should().Contain("code-a_1");
    }

    [Fact]
    public void RemoveRule_ExistingCode_ReturnsTrue()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        engine.AddRule(new FakeRule("code-to-remove"));

        bool removed = engine.RemoveRule("code-to-remove");

        removed.Should().BeTrue();
        engine.GetCatalog().Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_NonExistingCode_ReturnsFalse()
    {
        RuleEngine<TestContext> engine = CreateEngine();

        bool removed = engine.RemoveRule("non-existing");

        removed.Should().BeFalse();
    }

    [Fact]
    public void RemoveRule_NullOrEmpty_ReturnsFalse()
    {
        RuleEngine<TestContext> engine = CreateEngine();

        engine.RemoveRule("").Should().BeFalse();
        engine.RemoveRule("   ").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SingleRule_ExecutesSuccessfully()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        FakeRule rule = new("rule-1");
        engine.AddRule(rule);

        await engine.ExecuteAsync(new TestContext());

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RuleThrows_PropagatesException()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        engine.AddRule(new ThrowingRule("throws"));

        Func<Task> act = () => engine.ExecuteAsync(new TestContext());

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("Rule failed");
    }

    [Fact]
    public async Task ExecuteAsync_FilterByRuleType_OnlyRunsMatchingRules()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        FakeRule validationRule = new("validation-rule", RuleType.Validation);
        FakeRule businessRule = new("business-rule", RuleType.Business);
        engine.AddRule(validationRule);
        engine.AddRule(businessRule);

        await engine.ExecuteAsync(new TestContext(), RuleType.Validation);

        validationRule.ExecuteCount.Should().Be(1);
        businessRule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FilterBySelectedCodes_OnlyRunsSelectedRules()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        FakeRule rule1 = new("rule-1");
        FakeRule rule2 = new("rule-2");
        engine.AddRule(rule1);
        engine.AddRule(rule2);

        await engine.ExecuteAsync(new TestContext(), ["rule-1"]);

        rule1.ExecuteCount.Should().Be(1);
        rule2.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_RuleToggledOff_SkipsDisabledRule()
    {
        RuleOptions opts = new() { RuleToggles = { ["rule-1"] = false } };
        IOptionsMonitor<RuleOptions> monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
        monitor.CurrentValue.Returns(opts);

        RuleEngine<TestContext> engine = CreateEngine(options: monitor);
        FakeRule rule = new("rule-1");
        engine.AddRule(rule);

        await engine.ExecuteAsync(new TestContext());

        rule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationToken_Passes()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        FakeRule rule = new("rule-1");
        engine.AddRule(rule);

        using CancellationTokenSource cts = new();
        await engine.ExecuteAsync(new TestContext(), cts.Token);

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantDetection_Throws()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext("tenant-A", null, null, Guid.NewGuid().ToString("N"), null, null, false, [], "test"));

        RuleEngine<TestContext> engine = new(
            executionContextAccessor: accessor);
        engine.AddRule(new FakeRule("r1"));

        Func<Task> act = () => engine.ExecuteAsync(
            new TestContext { TenantId = "tenant-B" });

        await act.Should().ThrowAsync<Muonroi.Core.Abstractions.Exceptions.MUnauthorizedException>()
            .WithMessage("*Cross tenant*");
    }

    [Fact]
    public async Task ExecuteAsync_WithDependencies_ExecutesInOrder()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        List<string> executionOrder = [];

        engine.AddRule(
            new OrderTrackingRule("rule-B", executionOrder),
            new RuleDescriptor("rule-B", "B", "", RuleType.Validation, 1, DependsOn: ["rule-A"]));
        engine.AddRule(
            new OrderTrackingRule("rule-A", executionOrder),
            new RuleDescriptor("rule-A", "A", "", RuleType.Validation, 0));

        await engine.ExecuteAsync(new TestContext());

        executionOrder.Should().ContainInOrder("rule-A", "rule-B");
    }

    [Fact]
    public void AddRule_CircularDependency_ThrowsOnExecute()
    {
        RuleEngine<TestContext> engine = CreateEngine();
        engine.AddRule(
            new FakeRule("A"),
            new RuleDescriptor("A", "A", "", RuleType.Validation, DependsOn: ["B"]));
        engine.AddRule(
            new FakeRule("B"),
            new RuleDescriptor("B", "B", "", RuleType.Validation, DependsOn: ["A"]));

        Func<Task> act = () => engine.ExecuteAsync(new TestContext());

        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Circular*");
    }

    [Fact]
    public void GetCatalog_EmptyEngine_ReturnsEmpty()
    {
        RuleEngine<TestContext> engine = CreateEngine();

        engine.GetCatalog().Should().BeEmpty();
    }

    // --- Test helpers ---

    public sealed class TestContext
    {
        public string? TenantId { get; set; }
    }

    private sealed class FakeRule(string code, RuleType type = RuleType.Validation) : IRule<TestContext>
    {
        public string Code { get; } = code;
        public RuleType Type { get; } = type;
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRule(string code) : IRule<TestContext>
    {
        public string Code => code;

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            throw new MInternalException("Rule failed");
        }
    }

    private sealed class OrderTrackingRule(string code, List<string> tracker) : IRule<TestContext>
    {
        public string Code => code;

        public Task ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            tracker.Add(code);
            return Task.CompletedTask;
        }
    }
}
