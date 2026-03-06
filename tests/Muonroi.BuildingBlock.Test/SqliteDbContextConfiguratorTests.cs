namespace Muonroi.BuildingBlock.Test;

public class SqliteDbContextConfiguratorTests
{
    [Fact]
    public void Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqliteDbContextConfigurator<TestDbContext> cfg = new();

        cfg.Configure(builder, "DataSource=:memory:");

        string? connectionString = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()?.ConnectionString;

        Assert.NotNull(connectionString);
        Assert.Equal("DataSource=:memory:", connectionString);
    }

    [Fact]
    public void Configure_NullConnectionString_Throws()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqliteDbContextConfigurator<TestDbContext> cfg = new();
        string? cs = null;

        Assert.ThrowsAny<Exception>(() => cfg.Configure(builder, cs!));
    }

    [Fact]
    public void Configure_InvalidConnectionString_NoException()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        SqliteDbContextConfigurator<TestDbContext> cfg = new();

        cfg.Configure(builder, "invalid");

        string? connectionString = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()?.ConnectionString;

        Assert.NotNull(connectionString);
        Assert.Equal("invalid", connectionString);
    }
}
