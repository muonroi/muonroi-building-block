

namespace Muonroi.BuildingBlock.Test;

public class FaultyDbContext(DbContextOptions<FaultyDbContext> options) : MDbContext(options, new MultiTenant.FakeMediator())
{
    public override int SaveChanges()
    {
        throw new Exception("Database failure");
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new Exception("Database failure");
    }
}
