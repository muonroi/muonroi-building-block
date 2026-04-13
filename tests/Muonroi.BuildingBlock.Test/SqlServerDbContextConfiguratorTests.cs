namespace Muonroi.BuildingBlock.Test;

public class SqlServerDbContextConfiguratorTests
{
    [Fact]
    public void Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqlServerDbContextConfigurator<TestDbContext> cfg = new();
        string cs = "Server=(localdb)\\mssqllocaldb;Database=test;Trusted_Connection=True;";

        cfg.Configure(builder, cs);

        string? connectionString = builder.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault()
            ?.ConnectionString;
        Assert.NotNull(connectionString);
        Assert.Equal(cs, connectionString);
    }

    [Fact]
    public void Configure_NullConnectionString_Throws()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqlServerDbContextConfigurator<TestDbContext> cfg = new();
        string? cs = null;

        Assert.ThrowsAny<Exception>(() => cfg.Configure(builder, cs!));
    }

    [Fact]
    public void Configure_InvalidConnectionString_NoException()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqlServerDbContextConfigurator<TestDbContext> cfg = new();

        cfg.Configure(builder, "invalid");

        string? connectionString = builder.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault()
            ?.ConnectionString;
        Assert.NotNull(connectionString);
        Assert.Equal("invalid", connectionString);
    }
}
