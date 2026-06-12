namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class PostgreSqlDbContextConfiguratorTests
{
    private sealed class PgConfiguratorTestDbContext(DbContextOptions<PgConfiguratorTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void Configure_Sets_ConnectionString()
    {
        DbContextOptionsBuilder<PgConfiguratorTestDbContext> builder = new();
        PostgreSqlDbContextConfigurator<PgConfiguratorTestDbContext> configurator = new();
        const string connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        configurator.Configure(builder, connectionString);

        string? actual = builder.Options.Extensions
            .OfType<RelationalOptionsExtension>()
            .FirstOrDefault()
            ?.ConnectionString;

        actual.Should().NotBeNull();
        actual.Should().Be(connectionString);
    }

    [Fact]
    public void Configure_With_Valid_ConnectionString_Has_Extension()
    {
        DbContextOptionsBuilder<PgConfiguratorTestDbContext> builder = new();
        PostgreSqlDbContextConfigurator<PgConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "Host=localhost;Database=test");

        builder.Options.Extensions.Should().NotBeEmpty();
    }

    [Fact]
    public void Configure_Registers_Npgsql_Extension()
    {
        DbContextOptionsBuilder<PgConfiguratorTestDbContext> builder = new();
        PostgreSqlDbContextConfigurator<PgConfiguratorTestDbContext> configurator = new();

        configurator.Configure(builder, "Host=localhost;Database=test;Username=test;Password=test");

        builder.Options.Extensions
            .Any(ext => ext.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }
}
