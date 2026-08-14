namespace Muonroi.AspNetCore.Tests.Models;

public class RuleOrderChangeRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var request = new RuleOrderChangeRequest();

        request.EndpointRoute.Should().Be("/");
        request.TenantId.Should().BeNull();
        request.OrderedRuleCodes.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var request = new RuleOrderChangeRequest
        {
            EndpointRoute = "/api/v1/test",
            TenantId = "tenant-1",
            OrderedRuleCodes = ["rule-a", "rule-b"]
        };

        request.EndpointRoute.Should().Be("/api/v1/test");
        request.TenantId.Should().Be("tenant-1");
        request.OrderedRuleCodes.Should().HaveCount(2);
    }
}

public class RuleOrderChangeValidationResultTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var result = new RuleOrderChangeValidationResult();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }
}

public class RuleChangeRecordTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var record = new RuleChangeRecord();

        record.ChangeId.Should().NotBeEmpty();
        record.TenantId.Should().BeEmpty();
        record.EndpointRoute.Should().Be("/");
        record.PreviousOrder.Should().BeEmpty();
        record.NewOrder.Should().BeEmpty();
        record.AppliedBy.Should().Be("system");
    }
}

public class RuleOrderRollbackRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var request = new RuleOrderRollbackRequest();

        request.EndpointRoute.Should().Be("/");
        request.TenantId.Should().BeNull();
    }
}

public class RuleChangeProposalTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var proposal = new RuleChangeProposal();

        proposal.ProposalId.Should().NotBeEmpty();
        proposal.TenantId.Should().BeEmpty();
        proposal.EndpointRoute.Should().Be("/");
        proposal.OrderedRuleCodes.Should().BeEmpty();
        proposal.ProposedBy.Should().Be("system");
        proposal.Status.Should().Be(ProposalStatus.Pending);
        proposal.ReviewedBy.Should().BeNull();
        proposal.ReviewedAtUtc.Should().BeNull();
        proposal.ReviewNote.Should().BeNull();
    }
}

public class ProposeRuleChangeRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var request = new ProposeRuleChangeRequest();

        request.EndpointRoute.Should().Be("/");
        request.TenantId.Should().BeNull();
        request.OrderedRuleCodes.Should().BeEmpty();
        request.Note.Should().BeNull();
    }
}

public class ReviewProposalRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var request = new ReviewProposalRequest();
        request.ReviewNote.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var request = new ReviewProposalRequest { ReviewNote = "Approved with conditions" };
        request.ReviewNote.Should().Be("Approved with conditions");
    }
}

public class ProposalStatusTests
{
    [Theory]
    [InlineData(ProposalStatus.Pending, 0)]
    [InlineData(ProposalStatus.Approved, 1)]
    [InlineData(ProposalStatus.Rejected, 2)]
    public void EnumValues_AreCorrect(ProposalStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }
}
