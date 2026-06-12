using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Abstractions.Rules;
using Muonroi.RuleEngine.EntityFrameworkCore.Rules;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetApprovalServiceTests
{
    [Fact]
    public async Task SubmitForApprovalAsync_TransitionsDraftRecord_AndPublishesAudit()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.Draft);

        RuleSetRecord result = await fixture.Service.SubmitForApprovalAsync(" workflow-a ", 1, " reviewer ");

        result.Status.Should().Be(RuleSetStatus.PendingApproval);
        result.SubmittedBy.Should().Be("reviewer");
        fixture.AuditStore.Entries.Should().ContainSingle(x => x.Action == "SubmitForApproval" && x.Actor == "reviewer");
        fixture.Notifier.Events.Should().ContainSingle(x => x.ChangeType == RuleSetChangeTypes.SubmittedForApproval);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_WhenStatusInvalid_Throws()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.Active);

        Func<Task> action = async () => await fixture.Service.SubmitForApprovalAsync("workflow-a", 1, "reviewer");

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*cannot be submitted*");
    }

    [Fact]
    public async Task ApproveAsync_ApprovesPendingRecord_AndPreventsMakerCheckerViolation()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.PendingApproval, submittedBy: "maker");

        RuleSetRecord approved = await fixture.Service.ApproveAsync("workflow-a", 1, "checker");

        approved.Status.Should().Be(RuleSetStatus.Approved);
        approved.ApprovedBy.Should().Be("checker");
        fixture.AuditStore.Entries.Should().ContainSingle(x => x.Action == "Approve");
        fixture.Notifier.Events.Should().ContainSingle(x => x.ChangeType == RuleSetChangeTypes.Approved);
    }

    [Fact]
    public async Task ApproveAsync_WhenSubmitterMatchesApprover_ThrowsMakerCheckerViolation()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.PendingApproval, submittedBy: "maker");

        Func<Task> makerApprovesOwnRule = async () => await fixture.Service.ApproveAsync("workflow-a", 1, "maker");

        await makerApprovesOwnRule.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Maker-checker violation*");
    }

    [Fact]
    public async Task RejectAsync_RevertsPendingRecordToDraft_AndCapturesReason()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.PendingApproval);

        RuleSetRecord rejected = await fixture.Service.RejectAsync("workflow-a", 1, "checker", " not ready ");

        rejected.Status.Should().Be(RuleSetStatus.Draft);
        rejected.RejectedBy.Should().Be("checker");
        rejected.RejectedReason.Should().Be("not ready");
        fixture.AuditStore.Entries.Should().ContainSingle(x => x.Action == "Reject" && x.Detail == "not ready");
        fixture.Notifier.Events.Should().ContainSingle(x => x.ChangeType == RuleSetChangeTypes.Rejected);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_WithoutTenantContext_UsesDefaultTenant()
    {
        await using ApprovalFixture fixture = await ApprovalFixture.CreateAsync(RuleSetStatus.Draft, tenantId: "default", setTenantContext: false);

        RuleSetRecord result = await fixture.Service.SubmitForApprovalAsync("workflow-a", 1, "reviewer");

        result.TenantId.Should().Be("default");
        fixture.AuditStore.Entries.Should().ContainSingle(x => x.TenantId == "default");
    }

    private sealed class ApprovalFixture : IAsyncDisposable
    {
        private ApprovalFixture(
            RuleEngineDbContext dbContext,
            CapturingAuditStore auditStore,
            CapturingChangeNotifier notifier,
            RuleSetApprovalService service)
        {
            DbContext = dbContext;
            AuditStore = auditStore;
            Notifier = notifier;
            Service = service;
        }

        public RuleEngineDbContext DbContext { get; }
        public CapturingAuditStore AuditStore { get; }
        public CapturingChangeNotifier Notifier { get; }
        public RuleSetApprovalService Service { get; }

        public static async Task<ApprovalFixture> CreateAsync(
            RuleSetStatus status,
            string tenantId = "tenant-a",
            string? submittedBy = null,
            bool setTenantContext = true)
        {
            DbContextOptions<RuleEngineDbContext> options = new DbContextOptionsBuilder<RuleEngineDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            RuleEngineDbContext dbContext = new(options);

            dbContext.RuleSets.Add(new RuleSetRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowName = "workflow-a",
                Version = 1,
                Json = """{"workflowName":"workflow-a","rules":[]}""",
                Status = status,
                SubmittedBy = submittedBy
            });
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

            CapturingAuditStore auditStore = new();
            CapturingChangeNotifier notifier = new();
            RuleSetApprovalService service = new(dbContext, auditStore, notifier, accessor);
            return new ApprovalFixture(dbContext, auditStore, notifier, service);
        }

        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }

    private sealed class CapturingAuditStore : IRuleSetAuditStore
    {
        public List<RuleSetAuditEntry> Entries { get; } = [];

        public Task AppendAsync(RuleSetAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<RuleSetAuditPage> QueryAsync(string? workflowName = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuleSetAuditPage());
        }
    }

    private sealed class CapturingChangeNotifier : IRuleSetChangeNotifier
    {
        public List<RuleSetChangeEvent> Events { get; } = [];

        public Task PublishAsync(RuleSetChangeEvent changeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(changeEvent);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<RuleSetChangeEvent, Task> handler)
        {
            return new NoopDisposable();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class TestExecutionContextAccessor(ISystemExecutionContext context) : ISystemExecutionContextAccessor
    {
        private ISystemExecutionContext _context = context;

        public ISystemExecutionContext Get()
        {
            return _context;
        }

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
