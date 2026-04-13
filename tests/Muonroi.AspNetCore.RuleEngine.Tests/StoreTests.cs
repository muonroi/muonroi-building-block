namespace Muonroi.AspNetCore.RuleEngine.Tests;

public class StoreTests
{
    private readonly InMemoryRuleChangeStore _changeStore;
    private readonly InMemoryRuleChangeProposalStore _proposalStore;

    public StoreTests()
    {
        var dateTimeService = Substitute.For<IMDateTimeService>();
        dateTimeService.UtcNow().Returns(DateTime.UtcNow);
        _changeStore = new InMemoryRuleChangeStore(dateTimeService);
        _proposalStore = new InMemoryRuleChangeProposalStore(dateTimeService);
    }

    [Fact]
    public async Task ChangeStore_ApplyAndGet_ShouldWork()
    {
        // Arrange
        var request = new RuleOrderChangeRequest
        {
            TenantId = "tenant1",
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1", "rule2"]
        };

        // Act
        var record = await _changeStore.ApplyAsync(request, "user1", CancellationToken.None);
        var current = await _changeStore.GetCurrentAsync("tenant1", "/test", CancellationToken.None);
        var history = await _changeStore.GetHistoryAsync("tenant1", "/test", CancellationToken.None);

        // Assert
        record.NewOrder.Should().Equal("rule1", "rule2");
        current.Should().Equal("rule1", "rule2");
        history.Should().HaveCount(1);
    }

    [Fact]
    public async Task ChangeStore_Rollback_ShouldWork()
    {
        // Arrange
        var request1 = new RuleOrderChangeRequest
        {
            TenantId = "tenant1",
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1"]
        };
        await _changeStore.ApplyAsync(request1, "user1", CancellationToken.None);

        var request2 = new RuleOrderChangeRequest
        {
            TenantId = "tenant1",
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1", "rule2"]
        };
        await _changeStore.ApplyAsync(request2, "user1", CancellationToken.None);

        // Act
        var rolledBack = await _changeStore.RollbackAsync("tenant1", "/test", "user1", CancellationToken.None);
        var current = await _changeStore.GetCurrentAsync("tenant1", "/test", CancellationToken.None);

        // Assert
        rolledBack.Should().NotBeNull();
        rolledBack!.NewOrder.Should().Equal("rule1");
        current.Should().Equal("rule1");
    }

    [Fact]
    public async Task ProposalStore_ProposeApproveReject_ShouldWork()
    {
        // Arrange
        var request = new ProposeRuleChangeRequest
        {
            TenantId = "tenant1",
            EndpointRoute = "/test",
            OrderedRuleCodes = ["rule1"]
        };

        // Act & Assert - Propose
        var proposal = await _proposalStore.ProposeAsync(request, "proposer", CancellationToken.None);
        proposal.Status.Should().Be(ProposalStatus.Pending);

        var pending = await _proposalStore.ListPendingAsync("tenant1", CancellationToken.None);
        pending.Should().Contain(p => p.ProposalId == proposal.ProposalId);

        // Act & Assert - Approve
        var approved = await _proposalStore.ApproveAsync(proposal.ProposalId, "approver", "ok", CancellationToken.None);
        approved!.Status.Should().Be(ProposalStatus.Approved);

        // Act & Assert - Reject (new proposal)
        var proposal2 = await _proposalStore.ProposeAsync(request, "proposer", CancellationToken.None);
        var rejected = await _proposalStore.RejectAsync(proposal2.ProposalId, "rejecter", "no", CancellationToken.None);
        rejected!.Status.Should().Be(ProposalStatus.Rejected);
    }
}
