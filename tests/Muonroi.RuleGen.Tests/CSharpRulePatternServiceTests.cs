namespace Muonroi.RuleGen.Tests;

public sealed class CSharpRulePatternServiceTests
{
    [Fact]
    public void ExtractConditionAndAction_WithEmptyBody_ReturnsDefaultValues()
    {
        var result = CSharpRulePatternService.ExtractConditionAndAction("  ");

        result.ConditionFeel.Should().Be("true");
        result.ActionFeel.Should().BeEmpty();
        result.IsCustom.Should().BeTrue();
    }

    [Fact]
    public void ExtractConditionAndAction_WithoutIfStatement_ExtractsActionAssignmentsOnly()
    {
        var result = CSharpRulePatternService.ExtractConditionAndAction(
            """
            {
                facts["discount"] = ctx.Total > 100 ? 0.2m : 0.1m;
                FACTS["vip"] = true;
                other["ignored"] = false;
            }
            """);

        result.ConditionFeel.Should().Be("true");
        result.ActionFeel.Should().Contain("facts['discount']");
        result.ActionFeel.Should().Contain("facts['vip'] = true");
        result.ActionFeel.Should().NotContain("ignored");
        result.IsCustom.Should().BeTrue();
    }

    [Fact]
    public void ExtractConditionAndAction_WithFailureInIf_NegatesCondition()
    {
        var result = CSharpRulePatternService.ExtractConditionAndAction(
            """
            {
                if (ctx.Total > 100 && ctx.IsVip)
                {
                    return RuleResult.Failure("too-high");
                }

                facts["approved"] = true;
            }
            """);

        result.ConditionFeel.Should().Be("!(total > 100 and isVip)");
        result.ActionFeel.Should().Be("facts['approved'] = true");
        result.IsCustom.Should().BeFalse();
    }

    [Fact]
    public void ExtractConditionAndAction_WithLoopOrThisInvocation_MarksAsCustom()
    {
        var result = CSharpRulePatternService.ExtractConditionAndAction(
            """
            {
                if (ctx.Total > 0)
                {
                    foreach (var item in ctx.Items)
                    {
                        facts["count"] = 1;
                    }
                }

                this.Track();
            }
            """);

        result.ConditionFeel.Should().Be("total > 0");
        result.IsCustom.Should().BeTrue();
    }
}
