using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Rules.Tests.Rules;

public sealed class RuleEngineAdvancedTests
{
    private sealed class TenantContextModel : ITenantScoped
    {
        public string? TenantId { get; init; }
    }

    private sealed class TransactionalContextModel : ITransactionalRuleContext
    {
        public bool HasActiveTransaction { get; set; }
        public IDbContextTransaction? Transaction { get; set; }
        public int BeginCalls { get; private set; }
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            BeginCalls++;
            return Task.FromResult(Transaction);
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

    private sealed class CountingRule<TContext>(string code, RuleType type = RuleType.Validation) : IRule<TContext>
    {
        public int ExecuteCount { get; private set; }
        public string Code => code;
        public RuleType Type => type;

        public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRule<TContext>(string code, string message = "boom", RuleType type = RuleType.Validation)
        : IRule<TContext>
    {
        public string Code => code;
        public RuleType Type => type;

        public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(message);
    }

    [Fact]
    public async Task ExecuteAsync_WithTenantSpecificToggleDisabled_ShouldOverrideGlobalEnabledAndSkipRule()
    {
        var options = new RuleOptions
        {
            RuleToggles = { ["TENANT_RULE"] = true },
            TenantRuleToggles =
            {
                ["tenant-a"] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TENANT_RULE"] = false
                }
            }
        };

        var monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
        monitor.CurrentValue.Returns(options);

        var engine = new RuleEngine<TenantContextModel>(options: monitor);
        var rule = new CountingRule<TenantContextModel>("TENANT_RULE");
        engine.AddRule(rule);

        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-a";
        try
        {
            await engine.ExecuteAsync(new TenantContextModel { TenantId = "tenant-a" }, RuleType.Validation);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }

        rule.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithTenantSpecificToggleForDifferentTenant_ShouldIgnoreOverrideAndRunRule()
    {
        var options = new RuleOptions
        {
            RuleToggles = { ["TENANT_RULE"] = true },
            TenantRuleToggles =
            {
                ["tenant-b"] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TENANT_RULE"] = false
                }
            }
        };

        var monitor = Substitute.For<IOptionsMonitor<RuleOptions>>();
        monitor.CurrentValue.Returns(options);

        var engine = new RuleEngine<TenantContextModel>(options: monitor);
        var rule = new CountingRule<TenantContextModel>("TENANT_RULE");
        engine.AddRule(rule);

        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-a";
        try
        {
            await engine.ExecuteAsync(new TenantContextModel { TenantId = "tenant-a" }, RuleType.Validation);
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }

        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantScopedContext_ShouldThrowUnauthorizedAccessException()
    {
        var engine = new RuleEngine<TenantContextModel>();
        engine.AddRule(new CountingRule<TenantContextModel>("R1"));

        string? previousTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-a";
        try
        {
            Func<Task> act = () => engine.ExecuteAsync(new TenantContextModel { TenantId = "tenant-b" }, RuleType.Validation);
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*Cross tenant rule execution detected*");
        }
        finally
        {
            TenantContext.CurrentTenantId = previousTenant;
        }
    }

    [Fact]
    public async Task ExecuteAsync_TransactionalContextWithoutActiveTransaction_ShouldBeginAndCommit()
    {
        var context = new TransactionalContextModel
        {
            Transaction = Substitute.For<IDbContextTransaction>()
        };

        var engine = new RuleEngine<TransactionalContextModel>();
        var rule = new CountingRule<TransactionalContextModel>("TX_RULE");
        engine.AddRule(rule);

        await engine.ExecuteAsync(context, RuleType.Validation);

        context.BeginCalls.Should().Be(1);
        context.CommitCalls.Should().Be(1);
        context.RollbackCalls.Should().Be(0);
        rule.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransactionalContextWhenRuleFails_ShouldRollback()
    {
        var context = new TransactionalContextModel
        {
            Transaction = Substitute.For<IDbContextTransaction>()
        };

        var engine = new RuleEngine<TransactionalContextModel>();
        engine.AddRule(new ThrowingRule<TransactionalContextModel>("TX_FAIL"));

        Func<Task> act = () => engine.ExecuteAsync(context, RuleType.Validation);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("boom");
        context.BeginCalls.Should().Be(1);
        context.CommitCalls.Should().Be(0);
        context.RollbackCalls.Should().Be(1);
    }
}
