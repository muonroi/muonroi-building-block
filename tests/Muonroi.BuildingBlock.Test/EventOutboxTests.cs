using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class EventOutboxTests
{
    [Fact]
    public void Id_Returns_Value_Or_Default()
    {
        EventOutbox e = new();
        Assert.Equal(Guid.Empty, e.Id);
        Guid id = Guid.NewGuid();
        e.Id = id;
        Assert.Equal(id, e.Id);
    }

    [Fact]
    public void EventType_Returns_Value_Or_Empty()
    {
        EventOutbox e = new();
        Assert.Equal(string.Empty, e.EventType);
        e.EventType = "type";
        Assert.Equal("type", e.EventType);
    }

    [Fact]
    public void Payload_Returns_Value_Or_Empty()
    {
        EventOutbox e = new();
        Assert.Equal(string.Empty, e.Payload);
        e.Payload = "payload";
        Assert.Equal("payload", e.Payload);
    }

    [Fact]
    public void OccurredOn_Returns_Value_Or_Default()
    {
        EventOutbox e = new();
        Assert.Equal(default, e.OccurredOn);
        DateTime dt = DateTime.UtcNow;
        e.OccurredOn = dt;
        Assert.Equal(dt, e.OccurredOn);
    }

    [Fact]
    public void Published_Returns_Value_Or_Default()
    {
        EventOutbox e = new();
        Assert.False(e.Published);
        e.Published = true;
        Assert.True(e.Published);
    }
}

public class EventOutboxContextTests
{
    [Fact]
    public void Events_Returns_Set()
    {
        DbContextOptions<EventOutboxContext> options = new DbContextOptionsBuilder<EventOutboxContext>()
            .UseInMemoryDatabase("events_list")
            .Options;
        using EventOutboxContext ctx = new(options);
        Assert.NotNull(ctx.Events);
        Assert.Empty(ctx.Events);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<MArgumentException>(() => new EventOutboxContext(null!));
    }
}
