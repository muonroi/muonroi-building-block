namespace Muonroi.RuleEngine.Core.Tests.Events;

/// <summary>
/// Tests for CloudEvent CloudEvents 1.0 compliance and RuleCloudEventTypes constants.
/// </summary>
public sealed class CloudEventTests
{
    [Fact]
    public void CloudEvent_DefaultConstructor_PopulatesRequiredFields()
    {
        // Arrange & Act
        var evt = new CloudEvent
        {
            Type = RuleCloudEventTypes.RuleSetChanged,
            Source = "/muonroi/rule-engine"
        };

        // Assert — all CloudEvents 1.0 required fields
        Assert.NotNull(evt.Id);
        Assert.NotEmpty(evt.Id);
        Assert.Equal("1.0", evt.SpecVersion);
        Assert.Equal(RuleCloudEventTypes.RuleSetChanged, evt.Type);
        Assert.Equal("/muonroi/rule-engine", evt.Source);
        Assert.Equal("application/json", evt.DataContentType);
        Assert.NotEqual(default, evt.Time);
    }

    [Fact]
    public void CloudEvent_IdIsUniquePerInstance()
    {
        // Arrange
        var evt1 = new CloudEvent { Type = "t", Source = "s" };
        var evt2 = new CloudEvent { Type = "t", Source = "s" };

        // Assert
        Assert.NotEqual(evt1.Id, evt2.Id);
    }

    [Fact]
    public void CloudEvent_DataAcceptsObjectPayload()
    {
        // Arrange
        var payload = new Dictionary<string, string> { { "TenantId", "t1" }, { "WorkflowName", "OrderFlow" } };
        var evt = new CloudEvent
        {
            Type = RuleCloudEventTypes.RuleSetChanged,
            Source = "/test",
            Data = payload
        };

        // Assert
        Assert.NotNull(evt.Data);
        var dict = Assert.IsType<Dictionary<string, string>>(evt.Data);
        Assert.Equal("t1", dict["TenantId"]);
        Assert.Equal("OrderFlow", dict["WorkflowName"]);
    }

    [Fact]
    public void RuleCloudEventTypes_HasCorrectConstants()
    {
        // Assert all 4 type constants follow CloudEvents naming convention
        Assert.Equal("muonroi.ruleset.changed", RuleCloudEventTypes.RuleSetChanged);
        Assert.Equal("muonroi.ruleset.activated", RuleCloudEventTypes.RuleSetActivated);
        Assert.Equal("muonroi.rule.executed", RuleCloudEventTypes.RuleExecuted);
        Assert.Equal("muonroi.rule.failed", RuleCloudEventTypes.RuleFailed);
    }

    [Fact]
    public void CloudEvent_SubjectAndExtensionsAreOptional()
    {
        // Arrange & Act
        var evt = new CloudEvent
        {
            Type = RuleCloudEventTypes.RuleExecuted,
            Source = "/muonroi/rule-engine"
        };

        // Assert
        Assert.Null(evt.Subject);
        Assert.Null(evt.Extensions);
    }

    [Fact]
    public void CloudEvent_SubjectCanBeSet()
    {
        // Arrange & Act
        var evt = new CloudEvent
        {
            Type = RuleCloudEventTypes.RuleExecuted,
            Source = "/muonroi/rule-engine",
            Subject = "t1/OrderFlow"
        };

        // Assert
        Assert.Equal("t1/OrderFlow", evt.Subject);
    }

    [Fact]
    public void CloudEvent_SpecVersionIsAlways10()
    {
        // Arrange & Act
        var evt = new CloudEvent
        {
            Type = "t",
            Source = "s"
        };

        // Assert
        Assert.Equal("1.0", evt.SpecVersion);
    }
}
