using Xunit;
using FluentAssertions;

namespace Muonroi.Observability.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
