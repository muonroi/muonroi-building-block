namespace Muonroi.Messaging.Abstractions.Tests;

public class EventBaseTests
{
    private sealed class TestDomainEvent : DomainEvent
    {
    }

    private sealed class TestIntegrationEvent : IntegrationEvent
    {
    }

    [Fact]
    public void DomainEvent_OccurredOn_Is_UtcLike_Current_Time()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        TestDomainEvent domainEvent = new();

        domainEvent.OccurredOn.Should().BeOnOrAfter(before);
        domainEvent.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void IntegrationEvent_Creates_Id_And_OccurredOn()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        TestIntegrationEvent integrationEvent = new();

        integrationEvent.Id.Should().NotBe(Guid.Empty);
        integrationEvent.OccurredOn.Should().BeOnOrAfter(before);
        integrationEvent.OccurredOn.Kind.Should().Be(DateTimeKind.Utc);
    }
}
