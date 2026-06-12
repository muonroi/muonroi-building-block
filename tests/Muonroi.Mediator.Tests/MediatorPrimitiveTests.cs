namespace Muonroi.Mediator.Tests;

public sealed class MediatorPrimitiveTests
{
    [Fact]
    public void MUnauthorizedException_DefaultConstructor_UsesExpectedMessage()
    {
        MUnauthorizedException exception = new();

        exception.Message.Should().Be("Request requires an authenticated tenant context.");
    }

    [Fact]
    public void MUnauthorizedException_CustomConstructor_UsesCustomMessage()
    {
        MUnauthorizedException exception = new("custom");

        exception.Message.Should().Be("custom");
    }
}
