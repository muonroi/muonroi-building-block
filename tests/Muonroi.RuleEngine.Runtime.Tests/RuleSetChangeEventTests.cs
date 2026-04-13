using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetChangeEventTests
{
    [Fact]
    public void Record_Equality_SameValues()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RuleSetChangeEvent a = new("t1", "wf1", "saved", 1, now);
        RuleSetChangeEvent b = new("t1", "wf1", "saved", 1, now);

        a.Should().Be(b);
    }

    [Fact]
    public void Record_Inequality_DifferentValues()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RuleSetChangeEvent a = new("t1", "wf1", "saved", 1, now);
        RuleSetChangeEvent b = new("t2", "wf1", "saved", 1, now);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Properties_AreAccessible()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RuleSetChangeEvent evt = new("tenant-1", "my-workflow", RuleSetChangeTypes.Activated, 3, now);

        evt.TenantId.Should().Be("tenant-1");
        evt.WorkflowName.Should().Be("my-workflow");
        evt.ChangeType.Should().Be(RuleSetChangeTypes.Activated);
        evt.Version.Should().Be(3);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void NullVersion_IsAllowed()
    {
        RuleSetChangeEvent evt = new("t1", "wf1", "saved", null, DateTimeOffset.UtcNow);

        evt.Version.Should().BeNull();
    }
}

public sealed class RuleSetChangeTypesTests
{
    [Theory]
    [InlineData(RuleSetChangeTypes.Saved, "saved")]
    [InlineData(RuleSetChangeTypes.Activated, "activated")]
    [InlineData(RuleSetChangeTypes.SubmittedForApproval, "submitted_for_approval")]
    [InlineData(RuleSetChangeTypes.Approved, "approved")]
    [InlineData(RuleSetChangeTypes.Rejected, "rejected")]
    [InlineData(RuleSetChangeTypes.CanaryStarted, "canary_started")]
    [InlineData(RuleSetChangeTypes.CanaryPromoted, "canary_promoted")]
    [InlineData(RuleSetChangeTypes.CanaryRolledBack, "canary_rolled_back")]
    public void Constants_HaveExpectedValues(string actual, string expected)
    {
        actual.Should().Be(expected);
    }
}
