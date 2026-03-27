namespace Muonroi.Rules.Tests.Rules;

public class RuleDescriptorTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var descriptor = new RuleDescriptor(
            "R01", "Test Rule", "A test rule", RuleType.Validation, 5,
            ["DEP1", "DEP2"], false);

        descriptor.Code.Should().Be("R01");
        descriptor.Name.Should().Be("Test Rule");
        descriptor.Description.Should().Be("A test rule");
        descriptor.HookPoint.Should().Be(RuleType.Validation);
        descriptor.Order.Should().Be(5);
        descriptor.DependsOn.Should().BeEquivalentTo(["DEP1", "DEP2"]);
        descriptor.DefaultEnabled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultValues_ShouldBeApplied()
    {
        var descriptor = new RuleDescriptor("R01", "Name", "Desc", RuleType.Business);

        descriptor.Order.Should().Be(0);
        descriptor.DependsOn.Should().BeEmpty();
        descriptor.DefaultEnabled.Should().BeTrue();
    }

    [Fact]
    public void DependsOn_WhenNull_ShouldDefaultToEmptyArray()
    {
        var descriptor = new RuleDescriptor("R01", "Name", "Desc", RuleType.Validation, DependsOn: null);

        descriptor.DependsOn.Should().NotBeNull();
        descriptor.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public void Record_Equality_ShouldWorkCorrectly()
    {
        var a = new RuleDescriptor("R01", "Name", "Desc", RuleType.Validation);
        var b = new RuleDescriptor("R01", "Name", "Desc", RuleType.Validation);

        a.Should().Be(b);
    }

    [Fact]
    public void Record_WithExpression_ShouldCreateModifiedCopy()
    {
        var original = new RuleDescriptor("R01", "Name", "Desc", RuleType.Validation);
        var modified = original with { Code = "R02" };

        modified.Code.Should().Be("R02");
        modified.Name.Should().Be("Name");
    }
}
