using Xunit;
using FluentAssertions;

namespace Muonroi.Tenancy.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
