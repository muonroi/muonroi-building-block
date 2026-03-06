using Xunit;
using FluentAssertions;

namespace Muonroi.Kubernetes.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
