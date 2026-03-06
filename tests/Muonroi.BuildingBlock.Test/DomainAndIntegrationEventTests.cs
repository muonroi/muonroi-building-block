namespace Muonroi.BuildingBlock.Test;

public class DomainEventTests
{
    private sealed class TestDomainEvent : DomainEvent
    {
    }

    [Fact]
    public void OccurredOn_Is_Set_To_UtcNow()
    {
        DateTime before = DateTime.UtcNow;
        TestDomainEvent ev = new();
        DateTime after = DateTime.UtcNow;
        Assert.InRange(ev.OccurredOn, before, after);
        Assert.Equal(DateTimeKind.Utc, ev.OccurredOn.Kind);
    }
}

public class IntegrationEventTests
{
    private sealed class TestIntegrationEvent : IntegrationEvent
    {
    }

    [Fact]
    public void Id_Is_Generated_On_Creation()
    {
        TestIntegrationEvent ev = new();
        Assert.NotEqual(Guid.Empty, ev.Id);
    }

    [Fact]
    public void OccurredOn_Is_Set_To_UtcNow()
    {
        DateTime before = DateTime.UtcNow;
        TestIntegrationEvent ev = new();
        DateTime after = DateTime.UtcNow;
        Assert.InRange(ev.OccurredOn, before, after);
        Assert.Equal(DateTimeKind.Utc, ev.OccurredOn.Kind);
    }
}
