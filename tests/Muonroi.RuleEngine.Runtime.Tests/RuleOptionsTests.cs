using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleOptionsTests
{
    [Fact]
    public void Default_RuleToggles_IsEmpty()
    {
        RuleOptions opts = new();

        opts.RuleToggles.Should().BeEmpty();
    }

    [Fact]
    public void Default_TenantRuleToggles_IsEmpty()
    {
        RuleOptions opts = new();

        opts.TenantRuleToggles.Should().BeEmpty();
    }

    [Fact]
    public void RuleToggles_CaseInsensitive()
    {
        RuleOptions opts = new();
        opts.RuleToggles["MyRule"] = true;

        opts.RuleToggles.Should().ContainKey("myrule");
    }

    [Fact]
    public void TenantRuleToggles_CaseInsensitive()
    {
        RuleOptions opts = new();
        opts.TenantRuleToggles["TenantA"] = new Dictionary<string, bool> { ["Rule1"] = false };

        opts.TenantRuleToggles.Should().ContainKey("tenanta");
    }
}
