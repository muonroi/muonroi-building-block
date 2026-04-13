namespace Muonroi.Rules.Tests.Rules;

public class RuleOptionsTests
{
    [Fact]
    public void RuleToggles_Default_ShouldBeEmpty()
    {
        var options = new RuleOptions();

        options.RuleToggles.Should().NotBeNull();
        options.RuleToggles.Should().BeEmpty();
    }

    [Fact]
    public void RuleToggles_ShouldBeCaseInsensitive()
    {
        var options = new RuleOptions();
        options.RuleToggles["MyRule"] = true;

        options.RuleToggles.Should().ContainKey("myrule");
        options.RuleToggles["MYRULE"].Should().BeTrue();
    }

    [Fact]
    public void TenantRuleToggles_Default_ShouldBeEmpty()
    {
        var options = new RuleOptions();

        options.TenantRuleToggles.Should().NotBeNull();
        options.TenantRuleToggles.Should().BeEmpty();
    }

    [Fact]
    public void TenantRuleToggles_ShouldBeCaseInsensitive()
    {
        var options = new RuleOptions();
        options.TenantRuleToggles["TenantA"] = new Dictionary<string, bool> { ["Rule1"] = false };

        options.TenantRuleToggles.Should().ContainKey("tenanta");
    }
}
