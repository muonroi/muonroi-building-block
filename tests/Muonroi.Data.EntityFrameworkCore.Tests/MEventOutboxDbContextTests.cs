namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MEventOutboxDbContextTests
{
    [Fact]
    public async Task AddAsync_Adds_Outbox_Entry()
    {
        DbContextOptions<MEventOutboxDbContext> options = new DbContextOptionsBuilder<MEventOutboxDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using MEventOutboxDbContext db = new(options, new NoMediator(), new TestLicenseGuard());

        EventOutbox outbox = new()
        {
            EventName = "TestEvent",
            EventType = "TestType",
            EventContent = "{}",
            Status = EventOutboxStatus.Pending
        };

        await db.AddAsync(outbox);
        await db.SaveChangesAsync();

        int count = await db.OutboxEvents.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_Throws_On_Null()
    {
        DbContextOptions<MEventOutboxDbContext> options = new DbContextOptionsBuilder<MEventOutboxDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using MEventOutboxDbContext db = new(options, new NoMediator(), new TestLicenseGuard());

        Func<Task> act = () => db.AddAsync(null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public void OutboxEvents_Returns_DbSet()
    {
        DbContextOptions<MEventOutboxDbContext> options = new DbContextOptionsBuilder<MEventOutboxDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using MEventOutboxDbContext db = new(options, new NoMediator(), new TestLicenseGuard());

        db.OutboxEvents.Should().NotBeNull();
    }

    [Fact]
    public async Task IEventOutboxStore_EventOutboxes_Returns_Queryable()
    {
        DbContextOptions<MEventOutboxDbContext> options = new DbContextOptionsBuilder<MEventOutboxDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using MEventOutboxDbContext db = new(options, new NoMediator(), new TestLicenseGuard());
        IEventOutboxStore store = db;

        EventOutbox outbox = new()
        {
            EventName = "TestEvent",
            EventType = "TestType",
            EventContent = "{}",
            Status = EventOutboxStatus.Pending
        };
        await db.AddAsync(outbox);
        await db.SaveChangesAsync();

        int count = await store.EventOutboxes.CountAsync();
        count.Should().Be(1);
    }
}
