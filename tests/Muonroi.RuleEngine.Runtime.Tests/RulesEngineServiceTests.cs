using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using NSubstitute;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.RuleEngine.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RulesEngineServiceTests
{
    private readonly IRuleSetStore _store = Substitute.For<IRuleSetStore>();
    private readonly ILicenseGuard _licenseGuard = Substitute.For<ILicenseGuard>();
    private readonly IRuleSetRuntimeCache _runtimeCache = Substitute.For<IRuleSetRuntimeCache>();
    private readonly IRuleSetChangeNotifier _notifier = Substitute.For<IRuleSetChangeNotifier>();
    private readonly IRuleSetDefinitionValidator _validator = Substitute.For<IRuleSetDefinitionValidator>();
    private readonly ICanaryRolloutService _canaryRolloutService = Substitute.For<ICanaryRolloutService>();
    private readonly ISystemExecutionContextAccessor _execCtx;

    public RulesEngineServiceTests()
    {
        _execCtx = new SystemExecutionContextAccessor();
        _execCtx.Set(new SystemExecutionContext("test-tenant", null, null, Guid.NewGuid().ToString("N"), null, null, false, [], "test"));
    }

    private RulesEngineService CreateSut(IServiceProvider? serviceProvider = null)
    {
        return new RulesEngineService(
            store: _store,
            settings: null,
            licenseGuard: _licenseGuard,
            runtimeCache: _runtimeCache,
            notifier: _notifier,
            serviceProvider: serviceProvider,
            validator: _validator,
            canaryRolloutService: _canaryRolloutService,
            executionContextAccessor: _execCtx);
    }

    [Fact]
    public async Task SaveRuleSetAsync_ValidInput_SavesAndNotifies()
    {
        _validator.Validate("wf1", Arg.Any<string>())
            .Returns(new RuleSetValidationResult { WorkflowName = "wf1", Shape = "LegacyWorkflowObject" });

        RulesEngineService sut = CreateSut();
        await sut.SaveRuleSetAsync("wf1", """{"rules":[{"RuleName":"r1"}]}""");

        await _store.Received(1).SaveAsync("wf1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(e => e.WorkflowName == "wf1" && e.ChangeType == RuleSetChangeTypes.Saved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveRuleSetAsync_InvalidPayload_Throws()
    {
        RuleSetValidationResult invalid = new() { WorkflowName = "wf1" };
        invalid.Errors.Add(new RuleSetValidationIssue("E1", "Bad payload"));
        _validator.Validate("wf1", Arg.Any<string>()).Returns(invalid);

        RulesEngineService sut = CreateSut();

        Func<Task> act = () => sut.SaveRuleSetAsync("wf1", "bad");

        await act.Should().ThrowAsync<MConfigurationException>();
        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveVersionAsync_CallsStoreAndNotifies()
    {
        RulesEngineService sut = CreateSut();

        await sut.SetActiveVersionAsync("wf1", 3);

        await _store.Received(1).SetActiveVersionAsync("wf1", 3, Arg.Any<CancellationToken>());
        await _notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(e => e.ChangeType == RuleSetChangeTypes.Activated && e.Version == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateRuleSetAsync_ReturnsValidationResult()
    {
        _validator.Validate("wf1", Arg.Any<string>())
            .Returns(new RuleSetValidationResult { WorkflowName = "wf1", Shape = "CodeArray" });

        RulesEngineService sut = CreateSut();
        RuleSetValidationResult result = await sut.ValidateRuleSetAsync("wf1", """["Rule1"]""");

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("CodeArray");
    }

    [Fact]
    public async Task GetRuleSetAsync_DelegatesToStore()
    {
        _store.GetAsync("wf1", null, Arg.Any<CancellationToken>())
            .Returns("json-payload");

        RulesEngineService sut = CreateSut();
        string? result = await sut.GetRuleSetAsync("wf1");

        result.Should().Be("json-payload");
    }

    [Fact]
    public async Task GetRuleSetAsync_SpecificVersion_PassesVersion()
    {
        _store.GetAsync("wf1", 2, Arg.Any<CancellationToken>())
            .Returns("v2-payload");

        RulesEngineService sut = CreateSut();
        string? result = await sut.GetRuleSetAsync("wf1", 2);

        result.Should().Be("v2-payload");
    }

    [Fact]
    public async Task GetVersionsAsync_DelegatesToStore()
    {
        _store.GetVersionsAsync("wf1", Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);

        RulesEngineService sut = CreateSut();
        int[] result = await sut.GetVersionsAsync("wf1");

        result.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task GetActiveVersionAsync_DelegatesToStore()
    {
        _store.GetActiveVersionAsync("wf1", Arg.Any<CancellationToken>())
            .Returns(7);

        RulesEngineService sut = CreateSut();
        int? result = await sut.GetActiveVersionAsync("wf1");

        result.Should().Be(7);
    }

    [Fact]
    public async Task GetWorkflowsAsync_DelegatesToStore()
    {
        _store.GetWorkflowsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { "wf1", "wf2" });

        RulesEngineService sut = CreateSut();
        IReadOnlyList<string> result = await sut.GetWorkflowsAsync();

        result.Should().Equal("wf1", "wf2");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_MissingWorkflow_ReturnsSuccessWithEmptyFacts()
    {
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-missing", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-missing", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        RulesEngineService sut = CreateSut();
        OrchestratorResult result = await sut.ExecuteWithResultAsync("wf-missing", new { Amount = 12 });

        result.IsSuccess.Should().BeTrue();
        result.Facts.Should().NotBeNull();
        result.RuleResults.Should().BeEmpty();
        await _runtimeCache.Received(1).GetOrCreateAsync(
            "test-tenant",
            "wf-missing",
            Arg.Any<Func<Task<string?>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteWithResultAsync_WhenCanaryVersionExists_UsesVersionedStoreAndSkipsRuntimeCache()
    {
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-canary", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(3);
        _store.GetAsync("wf-canary", 3, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        RulesEngineService sut = CreateSut();
        OrchestratorResult result = await sut.ExecuteWithResultAsync("wf-canary", new { Amount = 12 });

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).GetAsync("wf-canary", 3, Arg.Any<CancellationToken>());
        await _runtimeCache.DidNotReceive().GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<Task<string?>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_ResolvesRuleAndReturnsFacts()
    {
        const string json = """
                            {
                              "workflowName": "wf-code",
                              "executionMode": "BestEffort",
                              "rules": [ "TEST_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-code", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-code", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IRule<TestExecutionContext>, TestSuccessRule>()
            .BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        OrchestratorResult result = await sut.ExecuteWithResultAsync("wf-code", new TestExecutionContext { Value = 6 });

        result.IsSuccess.Should().BeTrue();
        result.ExecutionMode.Should().Be(ExecutionMode.BestEffort);
        result.Facts.Get<int>("doubled").Should().Be(12);
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_WithUnknownRuleCode_Throws()
    {
        const string json = """
                            {
                              "workflowName": "wf-missing-rule",
                              "rules": [ "UNKNOWN_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-missing-rule", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-missing-rule", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IRule<TestExecutionContext>, TestSuccessRule>()
            .BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        Func<Task> action = () => sut.ExecuteWithResultAsync("wf-missing-rule", new TestExecutionContext { Value = 1 });

        await action.Should().ThrowAsync<MConfigurationException>()
            .WithMessage("*unknown rule code*");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_WithAmbiguousRuleCode_Throws()
    {
        const string json = """
                            {
                              "workflowName": "wf-ambiguous-rule",
                              "rules": [ "TEST_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-ambiguous-rule", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-ambiguous-rule", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IRule<TestExecutionContext>, TestSuccessRule>()
            .AddSingleton<IRule<TestExecutionContext>, AlternateTestSuccessRule>()
            .BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        Func<Task> action = () => sut.ExecuteWithResultAsync("wf-ambiguous-rule", new TestExecutionContext { Value = 1 });

        await action.Should().ThrowAsync<RuleEngineAmbiguousCodeException>();
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_WithoutServiceProvider_UsesReflectionDiscovery()
    {
        const string json = """
                            {
                              "workflowName": "wf-reflection-rule",
                              "rules": [ "REFLECTION_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-reflection-rule", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-reflection-rule", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        RulesEngineService sut = CreateSut(serviceProvider: null);
        OrchestratorResult result = await sut.ExecuteWithResultAsync("wf-reflection-rule", new ReflectionExecutionContext { Value = 5 });

        result.IsSuccess.Should().BeTrue();
        result.Facts.Get<int>("reflection").Should().Be(15);
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflow_WhenRuleFails_ThrowsInvalidOperationException()
    {
        const string json = """
                            {
                              "workflowName": "wf-failing-rule",
                              "rules": [ "FAIL_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-failing-rule", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-failing-rule", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IRule<TestExecutionContext>, TestFailingRule>()
            .BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        Func<Task> action = async () => await sut.ExecuteAsync("wf-failing-rule", new TestExecutionContext { Value = 2 });

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*boom*");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_UsesReflectionDiscovery_WhenDiHasNoRules()
    {
        const string json = """
                            {
                              "workflowName": "wf-reflection",
                              "rules": [ "REFLECTION_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-reflection", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-reflection", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        OrchestratorResult result = await sut.ExecuteWithResultAsync("wf-reflection", new ReflectionExecutionContext { Value = 5 });

        result.IsSuccess.Should().BeTrue();
        result.Facts.Get<int>("reflection").Should().Be(15);
    }

    [Fact]
    public async Task ExecuteWithResultAsync_CodeWorkflow_WhenNoImplementationsExist_Throws()
    {
        const string json = """
                            {
                              "workflowName": "wf-no-impl",
                              "rules": [ "NO_IMPL_RULE" ]
                            }
                            """;
        _canaryRolloutService.GetCanaryVersionForTenantAsync("wf-no-impl", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((int?)null);
        _runtimeCache.GetOrCreateAsync("test-tenant", "wf-no-impl", Arg.Any<Func<Task<string?>>>(), Arg.Any<CancellationToken>())
            .Returns(json);

        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        RulesEngineService sut = CreateSut(provider);
        Func<Task> action = () => sut.ExecuteWithResultAsync("wf-no-impl", new NoImplementationContext { Value = 7 });

        await action.Should().ThrowAsync<MConfigurationException>()
            .WithMessage("*no rule implementations were discovered*");
    }

    public sealed class TestExecutionContext
    {
        public int Value { get; init; }
    }

    public sealed class ReflectionExecutionContext
    {
        public int Value { get; init; }
    }

    public sealed class NoImplementationContext
    {
        public int Value { get; init; }
    }

    private sealed class TestSuccessRule : IRule<TestExecutionContext>
    {
        public string Code => "TEST_RULE";

        public Task<RuleResult> EvaluateAsync(TestExecutionContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("doubled", ctx.Value * 2);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class AlternateTestSuccessRule : IRule<TestExecutionContext>
    {
        public string Code => "TEST_RULE";

        public Task<RuleResult> EvaluateAsync(TestExecutionContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("alternate", ctx.Value + 1);
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class TestFailingRule : IRule<TestExecutionContext>
    {
        public string Code => "FAIL_RULE";

        public Task<RuleResult> EvaluateAsync(TestExecutionContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Failure("boom"));
        }
    }

    private sealed class ReflectionOnlyRule : IRule<ReflectionExecutionContext>
    {
        public string Code => "REFLECTION_RULE";

        public Task<RuleResult> EvaluateAsync(ReflectionExecutionContext ctx, FactBag facts, CancellationToken ct)
        {
            facts.Set("reflection", ctx.Value * 3);
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
