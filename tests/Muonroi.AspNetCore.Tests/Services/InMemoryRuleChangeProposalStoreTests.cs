using Muonroi.AspNetCore.Models.Changes;

namespace Muonroi.AspNetCore.Tests.Services;

public class InMemoryRuleChangeProposalStoreTests
{
    private readonly IMDateTimeService _dateTimeService = Substitute.For<IMDateTimeService>();
    private readonly InMemoryRuleChangeProposalStore _store;
    private readonly DateTime _fixedTime = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    public InMemoryRuleChangeProposalStoreTests()
    {
        _dateTimeService.UtcNow().Returns(_fixedTime);
        _store = new InMemoryRuleChangeProposalStore(_dateTimeService);
    }

    [Fact]
    public async Task ProposeAsync_CreatesProposalWithPendingStatus()
    {
        var request = new ProposeRuleChangeRequest
        {
            TenantId = "t1",
            EndpointRoute = "/api/test",
            OrderedRuleCodes = ["rule-1", "rule-2"],
            Note = "test note"
        };

        var proposal = await _store.ProposeAsync(request, "admin");

        proposal.TenantId.Should().Be("t1");
        proposal.EndpointRoute.Should().Be("/api/test");
        proposal.OrderedRuleCodes.Should().BeEquivalentTo(["rule-1", "rule-2"]);
        proposal.ProposedBy.Should().Be("admin");
        proposal.Status.Should().Be(ProposalStatus.Pending);
        proposal.ProposedAtUtc.Should().Be(_fixedTime);
        proposal.ReviewNote.Should().Be("test note");
    }

    [Fact]
    public async Task ProposeAsync_BlankProposedBy_DefaultsToSystem()
    {
        var proposal = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "  ");

        proposal.ProposedBy.Should().Be("system");
    }

    [Fact]
    public async Task ProposeAsync_FiltersWhitespaceRuleCodes()
    {
        var proposal = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a", "", "  ", "b"]
        }, "user");

        proposal.OrderedRuleCodes.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task ProposeAsync_DeduplicatesRuleCodes()
    {
        var proposal = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a", "A", "b"]
        }, "user");

        proposal.OrderedRuleCodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_ExistingProposal_ReturnsClone()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        var retrieved = await _store.GetAsync(created.ProposalId);

        retrieved.Should().NotBeNull();
        retrieved!.ProposalId.Should().Be(created.ProposalId);
    }

    [Fact]
    public async Task GetAsync_NonExistentId_ReturnsNull()
    {
        var result = await _store.GetAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_PendingProposal_SetsApproved()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        var approved = await _store.ApproveAsync(created.ProposalId, "reviewer", "looks good");

        approved.Should().NotBeNull();
        approved!.Status.Should().Be(ProposalStatus.Approved);
        approved.ReviewedBy.Should().Be("reviewer");
        approved.ReviewNote.Should().Be("looks good");
        approved.ReviewedAtUtc.Should().Be(_fixedTime);
    }

    [Fact]
    public async Task ApproveAsync_NonExistent_ReturnsNull()
    {
        var result = await _store.ApproveAsync(Guid.NewGuid(), "user", null);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_AlreadyApproved_DoesNotChangeStatus()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        await _store.ApproveAsync(created.ProposalId, "reviewer1", null);
        var secondApproval = await _store.ApproveAsync(created.ProposalId, "reviewer2", "another note");

        secondApproval!.ReviewedBy.Should().Be("reviewer1");
    }

    [Fact]
    public async Task RejectAsync_PendingProposal_SetsRejected()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        var rejected = await _store.RejectAsync(created.ProposalId, "reviewer", "not approved");

        rejected.Should().NotBeNull();
        rejected!.Status.Should().Be(ProposalStatus.Rejected);
        rejected.ReviewedBy.Should().Be("reviewer");
        rejected.ReviewNote.Should().Be("not approved");
    }

    [Fact]
    public async Task RejectAsync_NonExistent_ReturnsNull()
    {
        var result = await _store.RejectAsync(Guid.NewGuid(), "user", null);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RejectAsync_AlreadyRejected_DoesNotChange()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        await _store.RejectAsync(created.ProposalId, "reviewer1", null);
        var secondReject = await _store.RejectAsync(created.ProposalId, "reviewer2", null);

        secondReject!.ReviewedBy.Should().Be("reviewer1");
    }

    [Fact]
    public async Task ListPendingAsync_ReturnsOnlyPendingForTenant()
    {
        await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t1", OrderedRuleCodes = ["a"]
        }, "user");

        await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t1", OrderedRuleCodes = ["b"]
        }, "user");

        await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t2", OrderedRuleCodes = ["c"]
        }, "user");

        var pending = await _store.ListPendingAsync("t1");

        pending.Should().HaveCount(2);
        pending.Should().OnlyContain(p => p.TenantId == "t1");
    }

    [Fact]
    public async Task ListPendingAsync_ExcludesApprovedAndRejected()
    {
        var p1 = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t1", OrderedRuleCodes = ["a"]
        }, "user");

        var p2 = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t1", OrderedRuleCodes = ["b"]
        }, "user");

        await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = "t1", OrderedRuleCodes = ["c"]
        }, "user");

        await _store.ApproveAsync(p1.ProposalId, "r", null);
        await _store.RejectAsync(p2.ProposalId, "r", null);

        var pending = await _store.ListPendingAsync("t1");

        pending.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListPendingAsync_NullTenantId_NormalizesToGlobal()
    {
        await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            TenantId = null, OrderedRuleCodes = ["a"]
        }, "user");

        var pending = await _store.ListPendingAsync("_global");

        pending.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProposeAsync_CancellationToken_ThrowsWhenCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ApproveAsync_BlankReviewer_DefaultsToSystem()
    {
        var created = await _store.ProposeAsync(new ProposeRuleChangeRequest
        {
            OrderedRuleCodes = ["a"]
        }, "user");

        var approved = await _store.ApproveAsync(created.ProposalId, "  ", null);

        approved!.ReviewedBy.Should().Be("system");
    }
}
