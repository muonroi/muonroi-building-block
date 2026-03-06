using Xunit;

namespace Muonroi.RuleEngine.Abstractions.Tests;

public class RuleExtractionAttributesTests
{
    [Fact]
    public void ExtractAsRuleAttribute_ExposesMetadata()
    {
        var attr = new ExtractAsRuleAttribute("ORDER_VALIDATE")
        {
            Order = 10,
            HookPoint = HookPoint.BeforeCreate,
            DependsOn = ["ORDER_PRECHECK"]
        };

        Assert.Equal("ORDER_VALIDATE", attr.Code);
        Assert.Equal(10, attr.Order);
        Assert.Equal(HookPoint.BeforeCreate, attr.HookPoint);
        Assert.Single(attr.DependsOn);
    }

    [Fact]
    public void RuleModeAttribute_StoresSelectedMode()
    {
        var attr = new RuleModeAttribute(RuleExecutionMode.Hybrid);
        Assert.Equal(RuleExecutionMode.Hybrid, attr.Mode);
    }

    [Fact]
    public void RuleExecutionMode_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(RuleExecutionMode), RuleExecutionMode.Traditional));
        Assert.True(Enum.IsDefined(typeof(RuleExecutionMode), RuleExecutionMode.Rules));
        Assert.True(Enum.IsDefined(typeof(RuleExecutionMode), RuleExecutionMode.Hybrid));
        Assert.True(Enum.IsDefined(typeof(RuleExecutionMode), RuleExecutionMode.Shadow));
    }
}
