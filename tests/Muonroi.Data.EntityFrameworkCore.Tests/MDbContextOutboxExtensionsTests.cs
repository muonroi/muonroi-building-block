using Muonroi.Messaging.Abstractions.Events;
using NSubstitute;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MDbContextOutboxExtensionsTests
{
    private sealed class TestIntegrationEvent
    {
        public string Name { get; set; } = "test-event";
    }

    [Fact]
    public async Task SaveWithOutboxAsync_Persists_Event_To_Outbox()
    {
        DbContextOptions<MEventOutboxDbContext> options = new DbContextOptionsBuilder<MEventOutboxDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using MEventOutboxDbContext db = new(options, new NoMediator(), new TestLicenseGuard());

        IMJsonSerializeService jsonService = Substitute.For<IMJsonSerializeService>();
        jsonService.Serialize(Arg.Any<TestIntegrationEvent>()).Returns("{\"Name\":\"test-event\"}");

        TestIntegrationEvent evt = new();
        int result = await db.SaveWithOutboxAsync(evt, jsonService);

        result.Should().BeGreaterThan(0);
        int count = await db.OutboxEvents.CountAsync();
        count.Should().Be(1);

        EventOutbox outbox = await db.OutboxEvents.FirstAsync();
        outbox.EventName.Should().Be("TestIntegrationEvent");
        outbox.EventContent.Should().Be("{\"Name\":\"test-event\"}");
        outbox.Status.Should().Be(EventOutboxStatus.Pending);
    }
}
