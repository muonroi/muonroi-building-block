namespace Muonroi.Rules.Tests.Rules;

public class RuleSetChangeEventTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new RuleSetChangeEvent("tenant1", "workflow1", RuleSetChangeTypes.Saved, 3, now);

        evt.TenantId.Should().Be("tenant1");
        evt.WorkflowName.Should().Be("workflow1");
        evt.ChangeType.Should().Be("saved");
        evt.Version.Should().Be(3);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void Constructor_WithNullVersion_ShouldAllowNull()
    {
        var evt = new RuleSetChangeEvent("t", "w", RuleSetChangeTypes.Activated, null, DateTimeOffset.UtcNow);

        evt.Version.Should().BeNull();
    }

    [Fact]
    public void ChangeTypes_Constants_ShouldHaveExpectedValues()
    {
        RuleSetChangeTypes.Saved.Should().Be("saved");
        RuleSetChangeTypes.Activated.Should().Be("activated");
    }

    [Fact]
    public void Record_Equality_ShouldWork()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new RuleSetChangeEvent("t", "w", "saved", 1, now);
        var b = new RuleSetChangeEvent("t", "w", "saved", 1, now);

        a.Should().Be(b);
    }
}
