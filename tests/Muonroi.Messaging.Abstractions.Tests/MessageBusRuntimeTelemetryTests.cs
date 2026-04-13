namespace Muonroi.Messaging.Abstractions.Tests;

public class MessageBusRuntimeTelemetryTests
{
    [Theory]
    [InlineData("rabbitmq://localhost/queue", "rabbitmq")]
    [InlineData("kafka://broker/topic", "kafka")]
    [InlineData("https://example.test", "https")]
    public void ResolveTransport_Returns_Expected_Value(string uri, string expected)
    {
        string transport = MessageBusRuntimeTelemetry.ResolveTransport(new Uri(uri));

        transport.Should().Be(expected);
    }

    [Fact]
    public void ResolveTransport_Null_Returns_Unknown()
    {
        string transport = MessageBusRuntimeTelemetry.ResolveTransport(null);

        transport.Should().Be("unknown");
    }

    [Fact]
    public void Constants_Use_Stable_Names()
    {
        MessageBusRuntimeTelemetry.ActivitySourceName.Should().Be("Muonroi.BuildingBlock.MessageBus");
        MessageBusRuntimeTelemetry.MeterName.Should().Be("Muonroi.BuildingBlock.MessageBus");
    }
}
