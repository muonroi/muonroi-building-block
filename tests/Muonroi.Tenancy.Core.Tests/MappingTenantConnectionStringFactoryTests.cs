namespace Muonroi.Tenancy.Core.Tests;

using TenantConnectionStringsOptions = Muonroi.Tenancy.Abstractions.TenantConnectionStringsOptions;

public class MappingTenantConnectionStringFactoryTests
{
    [Fact]
    public void GetConnectionString_ReturnsMapped_WhenTenantExists()
    {
        TenantConnectionStringsOptions options = new()
        {
            ConnectionStrings = new Dictionary<string, string>
            {
                ["t1"] = "cs1"
            }
        };
        MappingTenantConnectionStringFactory factory = new(Options.Create(options), "default");

        string result = factory.GetConnectionString("t1");

        Assert.Equal("cs1", result);
    }

    [Fact]
    public void GetConnectionString_ReturnsDefault_WhenTenantMissing()
    {
        TenantConnectionStringsOptions options = new()
        {
            ConnectionStrings = new Dictionary<string, string>
            {
                ["t1"] = "cs1"
            }
        };
        MappingTenantConnectionStringFactory factory = new(Options.Create(options), "default");

        string result = factory.GetConnectionString("unknown");

        Assert.Equal("default", result);
    }
}
