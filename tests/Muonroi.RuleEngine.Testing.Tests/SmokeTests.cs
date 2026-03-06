using FluentAssertions;
using Xunit;

namespace Muonroi.RuleEngine.Testing.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}