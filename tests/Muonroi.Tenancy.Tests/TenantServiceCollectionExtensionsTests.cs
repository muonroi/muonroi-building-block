namespace Muonroi.Tenancy.Tests;

public class TenantServiceCollectionExtensionsTests
{
    private sealed class LicensedGate : ITenantLicenseFeatureGate
    {
        public bool HasFeature(string featureName)
        {
            return true;
        }
    }

    private sealed class CustomResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>("custom");
        }
    }

    [Fact]
    public void AddTenantContext_Registers_Services_When_Enabled()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:Enabled"] = "true"
            })
            .Build();
        ServiceCollection services = [];
        services.AddSingleton<ITenantLicenseFeatureGate, LicensedGate>();

        services.AddTenantContext(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<Muonroi.Tenancy.Core.Legacy.ITenantContext>().Should().NotBeNull();
        provider.GetService<ITenantIdResolver>().Should().NotBeNull();
        provider.GetService<TenantContextMiddleware>().Should().NotBeNull();
    }

    [Fact]
    public void AddTenantContext_Does_Not_Register_When_Disabled()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:Enabled"] = "false"
            })
            .Build();
        ServiceCollection services = [];
        services.AddSingleton<ITenantLicenseFeatureGate, LicensedGate>();

        services.AddTenantContext(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<Muonroi.Tenancy.Core.Legacy.ITenantContext>().Should().BeNull();
        provider.GetService<ITenantIdResolver>().Should().BeNull();
        provider.GetService<TenantContextMiddleware>().Should().BeNull();
    }

    [Fact]
    public void AddTenantIdResolver_Registers_Generic_Resolver()
    {
        ServiceCollection services = [];

        services.AddTenantIdResolver<CustomResolver>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITenantIdResolver>().Should().BeOfType<CustomResolver>();
    }
}
