using FluentAssertions;
using Xunit;
namespace Muonroi.Caching.Redis.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}