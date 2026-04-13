namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class SqlServerDbContextConfiguratorTests
{
    private sealed class SqlServerConfiguratorTestDbContext(DbContextOptions<SqlServerConfiguratorTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<SqlServerConfiguratorTestDbContext> builder = new();
        SqlServerDbContextConfigurator<SqlServerConfiguratorTestDbContext> configurator = new();
        const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=test;Trusted_Connection=True;";

        configurator.Configure(builder, connectionString);

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.NotNull(actual);
        Assert.Equal(connectionString, actual);
    }

    [Fact]
    public void Configure_NullConnectionString_Throws()
    {
        DbContextOptionsBuilder<SqlServerConfiguratorTestDbContext> builder = new();
        SqlServerDbContextConfigurator<SqlServerConfiguratorTestDbContext> configurator = new();
        string? connectionString = null;

        Assert.ThrowsAny<Exception>(() => configurator.Configure(builder, connectionString!));
    }

    [Fact]
    public void Configure_InvalidConnectionString_NoException()
    {
        DbContextOptionsBuilder<SqlServerConfiguratorTestDbContext> builder = new();
        SqlServerDbContextConfigurator<SqlServerConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "invalid");

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.NotNull(actual);
        Assert.Equal("invalid", actual);
    }
}
