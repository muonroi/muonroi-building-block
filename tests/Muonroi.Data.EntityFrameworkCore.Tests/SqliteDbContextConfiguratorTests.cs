namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class SqliteDbContextConfiguratorTests
{
    private sealed class SqliteConfiguratorTestDbContext(DbContextOptions<SqliteConfiguratorTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<SqliteConfiguratorTestDbContext> builder = new();
        SqliteDbContextConfigurator<SqliteConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "DataSource=:memory:");

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.NotNull(actual);
        Assert.Equal("DataSource=:memory:", actual);
    }

    [Fact]
    public async Task Configure_NullConnectionString_Throws()
    {
        DbContextOptionsBuilder<SqliteConfiguratorTestDbContext> builder = new();
        SqliteDbContextConfigurator<SqliteConfiguratorTestDbContext> configurator = new();
        string? connectionString = null;

        await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => configurator.Configure(builder, connectionString!)));
    }

    [Fact]
    public void Configure_InvalidConnectionString_NoException()
    {
        DbContextOptionsBuilder<SqliteConfiguratorTestDbContext> builder = new();
        SqliteDbContextConfigurator<SqliteConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "invalid");

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.NotNull(actual);
        Assert.Equal("invalid", actual);
    }
}
