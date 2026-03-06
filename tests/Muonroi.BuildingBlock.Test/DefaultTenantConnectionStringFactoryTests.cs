namespace Muonroi.BuildingBlock.Test;

public class DefaultTenantConnectionStringFactoryTests
{
    [Fact]
    public void GetConnectionString_Returns_Same_For_Any_Tenant()
    {
        DefaultTenantConnectionStringFactory factory = new("conn");
        Assert.Equal("conn", factory.GetConnectionString("t1"));
        Assert.Equal("conn", factory.GetConnectionString("unknown"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetConnectionString_Returns_Same_For_Null_Or_Empty(string? tenantId)
    {
        DefaultTenantConnectionStringFactory factory = new("conn");
        Assert.Equal("conn", factory.GetConnectionString(tenantId));
    }

    [Fact]
    public void GetConnectionString_Returns_Provided_Value()
    {
        DefaultTenantConnectionStringFactory factory = new("cs");
        string result = factory.GetConnectionString("t1");
        Assert.Equal("cs", result);
    }

    [Fact]
    public void GetConnectionString_Allows_Null_ConnectionString()
    {
        DefaultTenantConnectionStringFactory factory = new(null!);
        string result = factory.GetConnectionString(null);
        Assert.Null(result);
    }
}
