using Microsoft.EntityFrameworkCore.Storage;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Rules.Tests.Rules;

public class RuleEngineTests
{
    private sealed class EmptyCodeRule : IRule<string>
    {
        public string Code => "";
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingCodeRule : IRule<string>
    {
        public string Code => throw new InvalidOperationException("No code");
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class TestRule : IRule<string>
    {
        public string Code { get; set; } = "TEST";
        public RuleType Type { get; set; } = RuleType.Validation;
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private class FailingRule : IRule<string>
    {
        public string Code => "FAIL";
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Rule failed");
        }
    }

    private sealed class TenantScopedContext(string? tenantId) : ITenantScoped
    {
        public string? TenantId { get; } = tenantId;
    }

    private sealed class TransactionalContext : ITransactionalRuleContext
    {
        public bool HasActiveTransaction { get; set; }
        public IDbContextTransaction? BegunTransaction { get; set; }
        public int BeginCalls { get; private set; }
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            BeginCalls++;
            return Task.FromResult(BegunTransaction);
        }

        public Task CommitTransactionAsync(IDbContextTransaction transaction)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }

        public void RollbackTransaction()
        {
            RollbackCalls++;
        }
    }

    private sealed class TransactionRule : IRule<TransactionalContext>
    {
        public int ExecuteCount { get; private set; }
        public string Code => "TX";
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(TransactionalContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TenantScopedRule : IRule<TenantScopedContext>
    {
        public int ExecuteCount { get; private set; }
        public string Code => "TENANT_RULE";
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(TenantScopedContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTransactionRule : IRule<TransactionalContext>
    {
        public string Code => "TX_FAIL";
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(TransactionalContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Transactional failure");
        }
    }

    [Fact]
    public async Task AddRule_AndExecute_ShouldRunRule()
    {
        var engine = new RuleEngine<string>();
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("context", RuleType.Validation);

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void AddRule_ShouldReturnSameInstance_ForChaining()
    {
        var engine = new RuleEngine<string>();
        var result = engine.AddRule(new TestRule());

        result.Should().BeSameAs(engine);
    }

    [Fact]
    public async Task AddRule_WithDescriptor_ShouldUseDescriptor()
    {
        var engine = new RuleEngine<string>();
        var descriptor = new RuleDescriptor("CUSTOM", "Custom Rule", "Test", RuleType.Business);
        engine.AddRule(new TestRule(), descriptor);

        var catalog = engine.GetCatalog().ToList();
        catalog.Should().ContainSingle(d => d.Code == "CUSTOM");
    }

    [Fact]
    public void RemoveRule_ExistingRule_ShouldReturnTrue()
    {
        var engine = new RuleEngine<string>();
        engine.AddRule(new TestRule());

        engine.RemoveRule("TEST").Should().BeTrue();
    }

    [Fact]
    public void RemoveRule_NonExistingRule_ShouldReturnFalse()
    {
        var engine = new RuleEngine<string>();
        engine.RemoveRule("NONEXISTENT").Should().BeFalse();
    }

    [Fact]
    public void RemoveRule_NullOrEmpty_ShouldReturnFalse()
    {
        var engine = new RuleEngine<string>();
        engine.RemoveRule("").Should().BeFalse();
        engine.RemoveRule("  ").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FilterByRuleType_ShouldOnlyRunMatchingRules()
    {
        var engine = new RuleEngine<string>();
        var preRule = new TestRule { Code = "PRE", Type = RuleType.Validation };
        var postRule = new TestRule { Code = "POST", Type = RuleType.Business };
        engine.AddRule(preRule);
        engine.AddRule(postRule);

        await engine.ExecuteAsync("ctx", RuleType.Validation);

        preRule.ExecuteCount.Should().Be(1);
        postRule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FilterBySelectedCodes_ShouldOnlyRunSelectedRules()
    {
        var engine = new RuleEngine<string>();
        var rule1 = new TestRule { Code = "R1" };
        var rule2 = new TestRule { Code = "R2" };
        engine.AddRule(rule1);
        engine.AddRule(rule2);

        await engine.ExecuteAsync("ctx", new[] { "R1" }, RuleType.Validation);

        rule1.ExecuteCount.Should().Be(1);
        rule2.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithRuleTogglesDisabled_ShouldSkipRule()
    {
        var options = new RuleOptions();
        options.RuleToggles["TEST"] = false;

        var monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
        monitor.CurrentValue.Returns(options);

        var engine = new RuleEngine<string>(options: monitor);
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("ctx", RuleType.Validation);

        rule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithRuleTogglesEnabled_ShouldRunRule()
    {
        var options = new RuleOptions();
        options.RuleToggles["TEST"] = true;

        var monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
        monitor.CurrentValue.Returns(options);

        var engine = new RuleEngine<string>(options: monitor);
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("ctx", RuleType.Validation);

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FailingRule_ShouldThrow()
    {
        var engine = new RuleEngine<string>();
        engine.AddRule(new FailingRule());

        Func<Task> act = () => engine.ExecuteAsync("ctx", RuleType.Validation);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("Rule failed");
    }

    [Fact]
    public void GetCatalog_ShouldReturnAllRegisteredRules()
    {
        var engine = new RuleEngine<string>();
        engine.AddRule(new TestRule { Code = "R1" });
        engine.AddRule(new TestRule { Code = "R2" });

        var catalog = engine.GetCatalog().ToList();
        catalog.Should().HaveCount(2);
        catalog.Select(d => d.Code).Should().Contain(["R1", "R2"]);
    }

    [Fact]
    public async Task ExecuteAsync_RulesWithDependencies_ShouldExecuteInOrder()
    {
        var engine = new RuleEngine<string>();
        var executionOrder = new List<string>();

        var ruleA = Substitute.For<IRule<string>>();
        ruleA.Code.Returns("A");
        ruleA.Type.Returns(RuleType.Validation);
        ruleA.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => { executionOrder.Add("A"); return Task.CompletedTask; });

        var ruleB = Substitute.For<IRule<string>>();
        ruleB.Code.Returns("B");
        ruleB.Type.Returns(RuleType.Validation);
        ruleB.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => { executionOrder.Add("B"); return Task.CompletedTask; });

        engine.AddRule(ruleB, new RuleDescriptor("B", "B", "", RuleType.Validation, 0, ["A"]));
        engine.AddRule(ruleA, new RuleDescriptor("A", "A", "", RuleType.Validation, 0));

        await engine.ExecuteAsync("ctx", RuleType.Validation);

        executionOrder.Should().ContainInOrder("A", "B");
    }

    [Fact]
    public void ExecuteAsync_CircularDependency_ShouldThrow()
    {
        var engine = new RuleEngine<string>();

        var ruleA = Substitute.For<IRule<string>>();
        ruleA.Code.Returns("A");
        ruleA.Type.Returns(RuleType.Validation);

        var ruleB = Substitute.For<IRule<string>>();
        ruleB.Code.Returns("B");
        ruleB.Type.Returns(RuleType.Validation);

        engine.AddRule(ruleA, new RuleDescriptor("A", "A", "", RuleType.Validation, 0, ["B"]));
        engine.AddRule(ruleB, new RuleDescriptor("B", "B", "", RuleType.Validation, 0, ["A"]));

        Func<Task> act = () => engine.ExecuteAsync("ctx", RuleType.Validation);
        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Circular*");
    }

    [Fact]
    public void ExecuteAsync_MissingDependency_ShouldThrow()
    {
        var engine = new RuleEngine<string>();

        var ruleA = Substitute.For<IRule<string>>();
        ruleA.Code.Returns("A");
        ruleA.Type.Returns(RuleType.Validation);

        engine.AddRule(ruleA, new RuleDescriptor("A", "A", "", RuleType.Validation, 0, ["MISSING"]));

        Func<Task> act = () => engine.ExecuteAsync("ctx", RuleType.Validation);
        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Missing dependency*");
    }

    [Fact]
    public async Task AddRule_DuplicateCode_ShouldGenerateUniqueSuffix()
    {
        var engine = new RuleEngine<string>();
        engine.AddRule(new TestRule { Code = "DUP" });
        engine.AddRule(new TestRule { Code = "DUP" });

        var catalog = engine.GetCatalog().Select(d => d.Code).ToList();
        catalog.Should().HaveCount(2);
        catalog.Should().Contain("DUP");
        catalog.Should().Contain("DUP_1");
    }

    [Fact]
    public void AddRule_EmptyCode_ShouldFallbackToGeneratedTypeName()
    {
        var engine = new RuleEngine<string>();

        engine.AddRule(new EmptyCodeRule());

        engine.GetCatalog().Select(d => d.Code).Should().ContainSingle()
            .Which.Should().Be("EmptyCodeRule_1");
    }

    [Fact]
    public void AddRule_CodeGetterThrows_ShouldFallbackToGeneratedTypeName()
    {
        var engine = new RuleEngine<string>();

        engine.AddRule(new ThrowingCodeRule());
        engine.AddRule(new ThrowingCodeRule());

        engine.GetCatalog().Select(d => d.Code).Should().Contain(["ThrowingCodeRule_1", "ThrowingCodeRule_2"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithActivationStrategy_ShouldFilterRules()
    {
        var activation = Substitute.For<IRuleActivationStrategy<string>>();
        activation.IsActive(Arg.Any<IRule<string>>(), Arg.Any<string>()).Returns(false);

        var engine = new RuleEngine<string>(activation: activation);
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("ctx", RuleType.Validation);

        rule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NoRuleTypes_ShouldRunAllRules()
    {
        var engine = new RuleEngine<string>();
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("ctx");

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_EmptySelectedCodes_ShouldRunAllMatchingRules()
    {
        var engine = new RuleEngine<string>();
        var rule = new TestRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync("ctx", Array.Empty<string>(), RuleType.Validation);

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithTenantSpecificToggleDisabled_ShouldSkipRule()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = "tenant-a";
            var options = new RuleOptions();
            options.RuleToggles["TEST"] = true;
            options.TenantRuleToggles["tenant-a"] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["TEST"] = false
            };

            var monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
            monitor.CurrentValue.Returns(options);

            var engine = new RuleEngine<string>(options: monitor);
            var rule = new TestRule();
            engine.AddRule(rule);

            await engine.ExecuteAsync("ctx", RuleType.Validation);

            rule.ExecuteCount.Should().Be(0);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantContext_ShouldThrowUnauthorizedAccess()
    {
        string? previousTenant = TenantContext.CurrentTenantId;
        try
        {
            TenantContext.CurrentTenantId = "tenant-a";
            var engine = new RuleEngine<TenantScopedContext>();
            var rule = new TenantScopedRule();
            engine.AddRule(rule);

            Func<Task> act = () => engine.ExecuteAsync(new TenantScopedContext("tenant-b"), RuleType.Validation);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*Cross tenant*");
            rule.ExecuteCount.Should().Be(0);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }

    [Fact]
    public async Task ExecuteAsync_TransactionalContext_ShouldBeginCommitAndExecuteRule()
    {
        var transaction = Substitute.For<IDbContextTransaction>();
        var context = new TransactionalContext
        {
            HasActiveTransaction = false,
            BegunTransaction = transaction
        };
        var engine = new RuleEngine<TransactionalContext>();
        var rule = new TransactionRule();
        engine.AddRule(rule);

        await engine.ExecuteAsync(context, RuleType.Validation);

        context.BeginCalls.Should().Be(1);
        context.CommitCalls.Should().Be(1);
        context.RollbackCalls.Should().Be(0);
        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransactionalContextFailure_ShouldRollback()
    {
        var transaction = Substitute.For<IDbContextTransaction>();
        var context = new TransactionalContext
        {
            HasActiveTransaction = false,
            BegunTransaction = transaction
        };
        var engine = new RuleEngine<TransactionalContext>();
        engine.AddRule(new FailingTransactionRule());

        Func<Task> act = () => engine.ExecuteAsync(context, RuleType.Validation);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("Transactional failure");
        context.BeginCalls.Should().Be(1);
        context.CommitCalls.Should().Be(0);
        context.RollbackCalls.Should().Be(1);
    }
}
