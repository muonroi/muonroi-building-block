using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Tests that verify the immutability guard in RuleEngineDbContext:
/// - Published versions (PendingApproval, Approved, Active, Superseded) cannot have Json mutated
/// - Only forward status transitions are allowed
/// - SaveAsync always creates new versions (vault model)
/// - Rejection path (PendingApproval -> Draft) is allowed
/// </summary>
public sealed class PostgresRuleSetStoreImmutabilityTests
{
    // ── Json Immutability Tests ──────────────────────────────────────────────

    [Theory]
    [InlineData(RuleSetStatus.PendingApproval)]
    [InlineData(RuleSetStatus.Approved)]
    [InlineData(RuleSetStatus.Active)]
    [InlineData(RuleSetStatus.Superseded)]
    public async Task ModifyingJsonOnPublishedVersion_ThrowsInvalidOperationException(RuleSetStatus status)
    {
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(status);

        RuleSetRecord record = await fixture.DbContext.RuleSets
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        record.Json = """{"workflowName":"workflow-a","rules":[{"id":"new"}]}""";

        Func<Task> action = () => fixture.DbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task ModifyingJsonOnDraftVersion_Succeeds()
    {
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(RuleSetStatus.Draft);

        RuleSetRecord record = await fixture.DbContext.RuleSets
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        string newJson = """{"workflowName":"workflow-a","rules":[{"id":"updated"}]}""";
        record.Json = newJson;

        await fixture.DbContext.SaveChangesAsync();

        RuleSetRecord updated = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        updated.Json.Should().Be(newJson);
    }

    // ── Status Transition Tests ──────────────────────────────────────────────

    [Fact]
    public async Task SetActiveVersionAsync_SupersedesOldActive_Succeeds()
    {
        await using ImmutabilityFixture fixture =
            await ImmutabilityFixture.CreateAsync(RuleSetStatus.Active, seedSecondApproved: true);

        await fixture.Store.SetActiveVersionAsync("workflow-a", 2);

        RuleSetRecord v1 = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .FirstAsync(x => x.WorkflowName == "workflow-a" && x.Version == 1);
        RuleSetRecord v2 = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .FirstAsync(x => x.WorkflowName == "workflow-a" && x.Version == 2);

        v1.Status.Should().Be(RuleSetStatus.Superseded);
        v2.Status.Should().Be(RuleSetStatus.Active);
    }

    [Fact]
    public async Task StatusTransition_ActiveToDraft_Throws()
    {
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(RuleSetStatus.Active);

        RuleSetRecord record = await fixture.DbContext.RuleSets
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        record.Status = RuleSetStatus.Draft;

        Func<Task> action = () => fixture.DbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public async Task StatusTransition_PendingApprovalToDraft_Succeeds()
    {
        // Rejection path: PendingApproval -> Draft is a legitimate transition
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(RuleSetStatus.PendingApproval);

        RuleSetRecord record = await fixture.DbContext.RuleSets
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        record.Status = RuleSetStatus.Draft;
        record.RejectedBy = "checker";
        record.RejectedReason = "not ready";
        record.UpdatedAt = DateTimeOffset.UtcNow;

        // Should not throw — rejection path is allowed
        await fixture.DbContext.SaveChangesAsync();

        RuleSetRecord updated = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .FirstAsync(x => x.WorkflowName == "workflow-a");
        updated.Status.Should().Be(RuleSetStatus.Draft);
    }

    // ── Vault Model Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_AlwaysCreatesNewVersion_NeverModifiesExisting()
    {
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(RuleSetStatus.Draft);

        string json1 = """{"workflowName":"workflow-a","rules":[]}""";
        string json2 = """{"workflowName":"workflow-a","rules":[{"id":"v2"}]}""";

        // First save creates version 2 (fixture already has v1)
        await fixture.Store.SaveAsync("workflow-a", json2);

        List<RuleSetRecord> all = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.WorkflowName == "workflow-a")
            .OrderBy(x => x.Version)
            .ToListAsync();

        all.Should().HaveCount(2);
        all[0].Version.Should().Be(1);
        all[0].Json.Should().Be(json1);
        all[1].Version.Should().Be(2);
        all[1].Json.Should().Be(json2);
    }

    [Fact]
    public async Task SaveAsync_WithFreshWorkflow_CreatesVersionOne()
    {
        await using ImmutabilityFixture fixture = await ImmutabilityFixture.CreateAsync(RuleSetStatus.Draft);

        string json = """{"workflowName":"new-workflow","rules":[]}""";
        await fixture.Store.SaveAsync("new-workflow", json);

        RuleSetRecord record = await fixture.DbContext.RuleSets
            .AsNoTracking()
            .FirstAsync(x => x.WorkflowName == "new-workflow");

        record.Version.Should().Be(1);
        record.Json.Should().Be(json);
    }

    // ── Fixture ──────────────────────────────────────────────────────────────

    private sealed class ImmutabilityFixture : IAsyncDisposable
    {
        private ImmutabilityFixture(
            RuleEngineDbContext dbContext,
            PostgresRuleSetStore store)
        {
            DbContext = dbContext;
            Store = store;
        }

        public RuleEngineDbContext DbContext { get; }
        public PostgresRuleSetStore Store { get; }

        public static async Task<ImmutabilityFixture> CreateAsync(
            RuleSetStatus status,
            string tenantId = "tenant-a",
            bool setTenantContext = true,
            bool seedSecondApproved = false)
        {
            DbContextOptions<RuleEngineDbContext> options = new DbContextOptionsBuilder<RuleEngineDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            RuleEngineDbContext dbContext = new(options);

            // Seed v1 record directly (bypasses guard because this is EntityState.Added)
            dbContext.RuleSets.Add(new RuleSetRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowName = "workflow-a",
                Version = 1,
                Json = """{"workflowName":"workflow-a","rules":[]}""",
                Status = status,
                IsActive = status == RuleSetStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            if (seedSecondApproved)
            {
                // Seed v2 as Approved so SetActiveVersionAsync can activate it
                dbContext.RuleSets.Add(new RuleSetRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowName = "workflow-a",
                    Version = 2,
                    Json = """{"workflowName":"workflow-a","rules":[{"id":"v2"}]}""",
                    Status = RuleSetStatus.Approved,
                    IsActive = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();

            TestExecutionContextAccessor accessor = new(
                setTenantContext
                    ? new SystemExecutionContext(
                        tenantId,
                        userId: "user-1",
                        username: "tester",
                        correlationId: "corr-1",
                        accessToken: null,
                        apiKey: null,
                        isAuthenticated: true,
                        permissions: [],
                        sourceType: "tests")
                    : SystemExecutionContext.Empty);

            PostgresRuleSetStore store = new(dbContext, new RuleControlPlaneOptions(), accessor);
            return new ImmutabilityFixture(dbContext, store);
        }

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }

    private sealed class TestExecutionContextAccessor(ISystemExecutionContext context) : ISystemExecutionContextAccessor
    {
        private ISystemExecutionContext _context = context;

        public ISystemExecutionContext Get() => _context;

        public void Set(ISystemExecutionContext context)
        {
            _context = context;
        }

        public void Clear()
        {
            _context = SystemExecutionContext.Empty;
        }
    }
}
