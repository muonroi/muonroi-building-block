using Xunit;
using Muonroi.RuleEngine.Abstractions.Authoring;

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

    [Fact]
    public void RuleAuthoringAttributes_StoreOverrideMetadata()
    {
        var factAttr = new MRuleFactDescriptionAttribute("facts.customer")
        {
            Label = "Customer",
            Description = "Resolved customer payload",
            Example = "CUST-001"
        };

        var contextAttr = new MRuleContextDescriptionAttribute
        {
            Title = "Create Booking Request",
            Description = "Context before the rule executes"
        };

        Assert.Equal("facts.customer", factAttr.FactKey);
        Assert.Equal("Customer", factAttr.Label);
        Assert.Equal("Resolved customer payload", factAttr.Description);
        Assert.Equal("CUST-001", factAttr.Example);
        Assert.Equal("Create Booking Request", contextAttr.Title);
        Assert.Equal("Context before the rule executes", contextAttr.Description);
    }

    [Fact]
    public void RuleAuthoringManifestProvider_ExposesManifest()
    {
        var provider = new TestManifestProvider();

        MRuleAuthoringManifest manifest = provider.GetManifest();

        Assert.Equal("Test.Assembly", manifest.AssemblyName);
        Assert.Single(manifest.Rules);
        Assert.Equal("RULE_A", manifest.Rules[0].Code);
        Assert.Equal("facts.customer", manifest.Rules[0].ProducedFacts[0].Key);
    }

    private sealed class TestManifestProvider : IRuleAuthoringManifestProvider
    {
        public MRuleAuthoringManifest GetManifest()
        {
            return new MRuleAuthoringManifest
            {
                AssemblyName = "Test.Assembly",
                AssemblyVersion = "1.0.0",
                Rules =
                [
                    new MRuleAuthoringEntry
                    {
                        Code = "RULE_A",
                        ProducedFacts =
                        [
                            new MFactEntry
                            {
                                Key = "facts.customer",
                                Label = "Customer"
                            }
                        ]
                    }
                ]
            };
        }
    }
}
