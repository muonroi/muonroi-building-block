using FluentAssertions;
using Xunit;
namespace Muonroi.RuleEngine.Runtime.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}