namespace Muonroi.RuleEngine.Core.Tests;

/// <summary>
/// Tests that RuleOrchestrator enforces license gate via optional ILicenseGuard (GATE-01).
/// </summary>
public sealed class RuleOrchestratorLicenseGateTests
{
    private sealed class PassingRule : IRule<string>
    {
        public bool WasCalled { get; private set; }
        public string Name => "PassingRule";
        public string Code => "PassingRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task<RuleResult> EvaluateAsync(string context, FactBag facts, CancellationToken ct)
        {
            WasCalled = true;
            facts["executed"] = true;
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static RuleOrchestrator<string> BuildOrchestrator(PassingRule rule, ILicenseGuard? guard)
    {
        return new RuleOrchestrator<string>(
            rules: [rule],
            hooks: [],
            logger: null,
            jsonSerializeService: null,
            listeners: null,
            quotaTracker: null,
            tracer: null,
            contextAccessor: null,
            traceContext: null,
            licenseGuard: guard);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoLicenseGuard_ExecutesNormally()
    {
        PassingRule rule = new();
        RuleOrchestrator<string> orchestrator = BuildOrchestrator(rule, null);

        FactBag facts = await orchestrator.ExecuteAsync("ctx");

        facts["executed"].Should().Be(true);
        rule.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidLicenseGuard_ExecutesNormally()
    {
        PassingRule rule = new();
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        // EnsureFeature does not throw = license is valid
        RuleOrchestrator<string> orchestrator = BuildOrchestrator(rule, guard);

        FactBag facts = await orchestrator.ExecuteAsync("ctx");

        guard.Received(1).EnsureFeature(FreeTierFeatures.Premium.RuleEngine);
        facts["executed"].Should().Be(true);
        rule.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithLicenseGuard_ThrowsWhenLicenseDenied()
    {
        PassingRule rule = new();
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        guard.When(g => g.EnsureFeature(FreeTierFeatures.Premium.RuleEngine))
             .Throw(new InvalidOperationException("License denied"));

        RuleOrchestrator<string> orchestrator = BuildOrchestrator(rule, guard);

        Func<Task> act = () => orchestrator.ExecuteAsync("ctx");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("License denied");
    }

    [Fact]
    public async Task ExecuteAsync_WithLicenseGuard_DoesNotCallRulesWhenDenied()
    {
        PassingRule rule = new();
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        guard.When(g => g.EnsureFeature(FreeTierFeatures.Premium.RuleEngine))
             .Throw(new InvalidOperationException("License denied"));

        RuleOrchestrator<string> orchestrator = BuildOrchestrator(rule, guard);

        try
        {
            await orchestrator.ExecuteAsync("ctx");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        rule.WasCalled.Should().BeFalse("rules must not execute when license is denied");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_WithLicenseGuard_ThrowsWhenLicenseDenied()
    {
        PassingRule rule = new();
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        guard.When(g => g.EnsureFeature(FreeTierFeatures.Premium.RuleEngine))
             .Throw(new InvalidOperationException("License denied"));

        RuleOrchestrator<string> orchestrator = BuildOrchestrator(rule, guard);

        Func<Task> act = () => orchestrator.ExecuteWithResultAsync("ctx");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("License denied");
    }
}
