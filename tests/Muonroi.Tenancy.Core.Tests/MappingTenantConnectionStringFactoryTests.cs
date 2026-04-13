using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;

namespace Muonroi.Tenancy.Core.Tests;

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
