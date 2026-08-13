using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class DbContextConfiguratorTests
{
    private sealed class ConfiguratorTestDbContext(DbContextOptions<ConfiguratorTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void PostgreSql_Configure_Adds_Npgsql_Extension()
    {
        DbContextOptionsBuilder<ConfiguratorTestDbContext> builder = new();
        PostgreSqlDbContextConfigurator<ConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "Host=localhost;Database=test;Username=test;Password=test");

        Assert.Contains(
            builder.Options.Extensions,
            extension => extension.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SqlServer_Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<ConfiguratorTestDbContext> builder = new();
        SqlServerDbContextConfigurator<ConfiguratorTestDbContext> configurator = new();
        const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=test;Trusted_Connection=True;";

        configurator.Configure(builder, connectionString);

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.Equal(connectionString, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlServer_Configure_Invalid_ConnectionString_Throws(string? connectionString)
    {
        DbContextOptionsBuilder<ConfiguratorTestDbContext> builder = new();
        SqlServerDbContextConfigurator<ConfiguratorTestDbContext> configurator = new();

        Assert.Throws<MArgumentException>((Action)(() => configurator.Configure(builder, connectionString!)));
    }

    [Fact]
    public void Sqlite_Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<ConfiguratorTestDbContext> builder = new();
        SqliteDbContextConfigurator<ConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "DataSource=:memory:");

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        Assert.Equal("DataSource=:memory:", actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sqlite_Configure_Invalid_ConnectionString_Throws(string? connectionString)
    {
        DbContextOptionsBuilder<ConfiguratorTestDbContext> builder = new();
        SqliteDbContextConfigurator<ConfiguratorTestDbContext> configurator = new();

        Assert.Throws<MArgumentException>((Action)(() => configurator.Configure(builder, connectionString!)));
    }
}
