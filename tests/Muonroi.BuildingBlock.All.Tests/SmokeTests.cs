using Xunit;
using FluentAssertions;

namespace Muonroi.BuildingBlock.All.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
