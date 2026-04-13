using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class DbContextConfiguratorTests
{
    [Fact]
    public void PostgreSql_Configure_Adds_Extension()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        PostgreSqlDbContextConfigurator<TestDbContext> cfg = new();
        cfg.Configure(builder, "Host=localhost;Database=db");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Npgsql"));
    }

    [Fact]
    public void PostgreSql_NullOptions_Throws()
    {
        PostgreSqlDbContextConfigurator<TestDbContext> cfg = new();
        Assert.Throws<MArgumentException>(() => cfg.Configure(null!, "cs"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PostgreSql_InvalidConnection_Still_Configures(string? cs)
    {
        PostgreSqlDbContextConfigurator<TestDbContext> cfg = new();
        DbContextOptionsBuilder<TestDbContext> builder = new();
        cfg.Configure(builder, cs!);
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Npgsql"));
    }

    [Fact]
    public void MySql_NullOptions_Throws()
    {
        MySqlDbContextConfigurator<TestDbContext> cfg = new();
        Assert.ThrowsAny<Exception>(() => cfg.Configure(null!, "cs"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MySql_InvalidConnection_Throws(string? cs)
    {
        MySqlDbContextConfigurator<TestDbContext> cfg = new();
        DbContextOptionsBuilder<TestDbContext> builder = new();
        Assert.ThrowsAny<Exception>(() => cfg.Configure(builder, cs!));
    }
}
