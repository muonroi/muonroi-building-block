namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleDescriptorTests
{
    [Fact]
    public void Constructor_DefaultValues()
    {
        RuleDescriptor desc = new("code-1", "Rule 1", "desc", RuleType.Validation);

        desc.Code.Should().Be("code-1");
        desc.Name.Should().Be("Rule 1");
        desc.Description.Should().Be("desc");
        desc.HookPoint.Should().Be(RuleType.Validation);
        desc.Order.Should().Be(0);
        desc.DependsOn.Should().BeEmpty();
        desc.DefaultEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDependencies()
    {
        RuleDescriptor desc = new("code-1", "Rule 1", "", RuleType.Business,
            Order: 5,
            DependsOn: ["dep-1", "dep-2"],
            DefaultEnabled: false);

        desc.Order.Should().Be(5);
        desc.DependsOn.Should().BeEquivalentTo(["dep-1", "dep-2"]);
        desc.DefaultEnabled.Should().BeFalse();
    }

    [Fact]
    public void Record_Equality()
    {
        RuleDescriptor a = new("code", "name", "desc", RuleType.Validation);
        RuleDescriptor b = new("code", "name", "desc", RuleType.Validation);

        a.Should().Be(b);
    }

    [Fact]
    public void NullDependsOn_DefaultsToEmpty()
    {
        RuleDescriptor desc = new("code", "name", "desc", RuleType.Validation, DependsOn: null);

        desc.DependsOn.Should().NotBeNull();
        desc.DependsOn.Should().BeEmpty();
    }
}
