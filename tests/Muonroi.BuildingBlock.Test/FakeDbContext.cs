namespace Muonroi.BuildingBlock.Test
{
    public class FakeDbContext(DbContextOptions options) : MDbContext(options, new FakeMediator())
    {
        public override DbSet<MRefreshToken> RefreshTokens { get; set; }

    }

}
