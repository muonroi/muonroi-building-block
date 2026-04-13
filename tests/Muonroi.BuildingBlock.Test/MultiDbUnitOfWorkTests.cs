namespace Muonroi.BuildingBlock.Test;

public class MultiDbUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAcrossContexts()
    {
        DbContextOptions<DbContext> options1 = new DbContextOptionsBuilder<DbContext>().UseInMemoryDatabase("db1").Options;
        DbContextOptions<DbContext> options2 = new DbContextOptionsBuilder<DbContext>().UseInMemoryDatabase("db2").Options;

        using DbContext db1 = new(options1);
        using DbContext db2 = new(options2);

        MultiDbUnitOfWork uow = new(db1, db2);
        int result = await uow.SaveChangesAsync();
        Assert.Equal(0, result);
    }

    private class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
    {
        public bool Disposed { get; private set; }

        public override void Dispose()
        {
            Disposed = true;
            base.Dispose();
        }
    }

    [Fact]
    public void Dispose_Handles_Multiple_Calls()
    {
        DbContextOptions<TrackingDbContext> opt1 = new DbContextOptionsBuilder<TrackingDbContext>().UseInMemoryDatabase("dispose1").Options;
        DbContextOptions<TrackingDbContext> opt2 = new DbContextOptionsBuilder<TrackingDbContext>().UseInMemoryDatabase("dispose2").Options;

        TrackingDbContext db1 = new(opt1);
        TrackingDbContext db2 = new(opt2);

        MultiDbUnitOfWork uow = new(db1, db2);
        uow.Dispose();

        Assert.True(db1.Disposed);
        Assert.True(db2.Disposed);
    }
}
