using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Adapters;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class SubFlowCycleExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        SubFlowCycleException ex = new("test message");

        ex.Message.Should().Be("test message");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        SubFlowCycleException ex = new("msg");

        ex.Should().BeAssignableTo<InvalidOperationException>();
    }
}
