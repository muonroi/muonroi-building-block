
using FluentAssertions;
using Xunit;
namespace Muonroi.AspNetCore.OpenApi.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}