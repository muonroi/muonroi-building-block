namespace Muonroi.Tenancy.Core.Tests;

public class TenantSchemaSelectorTests
{
    private static TenantSchemaSelector CreateSelector(TenantIsolationStrategy strategy = TenantIsolationStrategy.SeparateSchema)
    {
        MultiTenantOptions options = new() { Strategy = strategy };
        return new TenantSchemaSelector(Options.Create(options));
    }

    [Fact]
    public void ResolveSchema_Returns_Dbo_When_Strategy_Is_Not_SeparateSchema()
    {
        TenantSchemaSelector selector = CreateSelector(TenantIsolationStrategy.SharedSchema);

        string schema = selector.ResolveSchema("tenant-1");

        schema.Should().Be("dbo");
    }

    [Fact]
    public void ResolveSchema_Returns_Dbo_When_TenantId_Is_Null()
    {
        TenantSchemaSelector selector = CreateSelector();

        string schema = selector.ResolveSchema(null);

        schema.Should().Be("dbo");
    }

    [Fact]
    public void ResolveSchema_Returns_Dbo_When_TenantId_Is_Whitespace()
    {
        TenantSchemaSelector selector = CreateSelector();

        string schema = selector.ResolveSchema("   ");

        schema.Should().Be("dbo");
    }

    [Fact]
    public void ResolveSchema_Normalizes_TenantId_To_Lowercase_And_Replaces_Special_Chars()
    {
        TenantSchemaSelector selector = CreateSelector();

        string schema = selector.ResolveSchema("My-Tenant.Name Here");

        schema.Should().Be("my_tenant_name_here");
    }

    [Fact]
    public void ApplyToConnectionString_Returns_Original_When_Not_SeparateSchema()
    {
        TenantSchemaSelector selector = CreateSelector(TenantIsolationStrategy.SharedSchema);

        string result = selector.ApplyToConnectionString("Host=localhost;Database=test", "tenant-1");

        result.Should().Be("Host=localhost;Database=test");
    }

    [Fact]
    public void ApplyToConnectionString_Returns_Original_When_ConnectionString_Is_Empty()
    {
        TenantSchemaSelector selector = CreateSelector();

        string result = selector.ApplyToConnectionString("", "tenant-1");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ApplyToConnectionString_Returns_Original_When_SearchPath_Already_Present()
    {
        TenantSchemaSelector selector = CreateSelector();
        string conn = "Host=localhost;Database=test;SearchPath=existing";

        string result = selector.ApplyToConnectionString(conn, "tenant-1");

        result.Should().Be(conn);
    }

    [Fact]
    public void ApplyToConnectionString_Returns_Original_When_Search_Path_Already_Present()
    {
        TenantSchemaSelector selector = CreateSelector();
        string conn = "Host=localhost;Database=test;Search Path=existing";

        string result = selector.ApplyToConnectionString(conn, "tenant-1");

        result.Should().Be(conn);
    }

    [Fact]
    public void ApplyToConnectionString_Appends_SearchPath_For_Postgres_Connection()
    {
        TenantSchemaSelector selector = CreateSelector();
        string conn = "Host=localhost;Database=test";

        string result = selector.ApplyToConnectionString(conn, "tenant-1");

        result.Should().Be("Host=localhost;Database=test;SearchPath=tenant_1");
    }

    [Fact]
    public void ApplyToConnectionString_Trims_Trailing_Semicolon_Before_Appending()
    {
        TenantSchemaSelector selector = CreateSelector();
        string conn = "Host=localhost;Database=test;";

        string result = selector.ApplyToConnectionString(conn, "tenant-1");

        result.Should().Be("Host=localhost;Database=test;SearchPath=tenant_1");
    }

    [Fact]
    public void ApplyToConnectionString_Returns_Original_When_Not_Postgres_Style()
    {
        TenantSchemaSelector selector = CreateSelector();
        string conn = "Server=localhost;Database=test";

        string result = selector.ApplyToConnectionString(conn, "tenant-1");

        result.Should().Be(conn);
    }
}
