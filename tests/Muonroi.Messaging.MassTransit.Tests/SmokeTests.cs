using Xunit;
using FluentAssertions;

namespace Muonroi.Messaging.MassTransit.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
