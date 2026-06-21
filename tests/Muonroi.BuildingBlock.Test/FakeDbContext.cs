namespace Muonroi.BuildingBlock.Test
{
    public class FakeDbContext(DbContextOptions options) : MDbContext(options, new MultiTenant.FakeMediator())
    {
        public override DbSet<MRefreshToken> RefreshTokens { get; set; }

    }

}
