namespace Muonroi.Messaging.Abstractions.Tests;

public class EventOutboxTests
{
    [Fact]
    public void EventName_Returns_Value_Or_Null()
    {
        EventOutbox outbox = new();

        outbox.EventName.Should().BeNull();

        outbox.EventName = "user.created";

        outbox.EventName.Should().Be("user.created");
    }

    [Fact]
    public void EventContent_Returns_Value_Or_Null()
    {
        EventOutbox outbox = new();

        outbox.EventContent.Should().BeNull();

        outbox.EventContent = "{\"id\":1}";

        outbox.EventContent.Should().Be("{\"id\":1}");
    }

    [Fact]
    public void EventType_Returns_Value_Or_Null()
    {
        EventOutbox outbox = new();

        outbox.EventType.Should().BeNull();

        outbox.EventType = "integration";

        outbox.EventType.Should().Be("integration");
    }

    [Fact]
    public void Status_Defaults_To_Pending()
    {
        EventOutbox outbox = new();

        outbox.Status.Should().Be(EventOutboxStatus.Pending);
    }

    [Fact]
    public void ErrorMessage_Returns_Value_Or_Null()
    {
        EventOutbox outbox = new();

        outbox.ErrorMessage.Should().BeNull();

        outbox.ErrorMessage = "failed";

        outbox.ErrorMessage.Should().Be("failed");
    }
}
