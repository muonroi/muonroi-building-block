namespace Muonroi.BuildingBlock.Test;

public class TenantServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTenantContext_Registers_Services_When_Enabled()
    {
        Dictionary<string, string?> data = new()
        {
            ["MultiTenantConfigs:Enabled"] = "true"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddTenantContext(config);
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ITenantContext>());
        Assert.NotNull(provider.GetService<ITenantIdResolver>());
        Assert.NotNull(provider.GetService<TenantContextMiddleware>());
    }

    [Fact]
    public void AddTenantContext_Does_Not_Register_When_Disabled()
    {
        Dictionary<string, string?> data = new()
        {
            ["MultiTenantConfigs:Enabled"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddTenantContext(config);
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ITenantContext>());
        Assert.Null(provider.GetService<ITenantIdResolver>());
        Assert.Null(provider.GetService<TenantContextMiddleware>());
    }

    private class CustomResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>("custom");
        }
    }

    [Fact]
    public void AddTenantIdResolver_Registers_Generic()
    {
        ServiceCollection services = [];
        services.AddTenantIdResolver<CustomResolver>();
        ServiceProvider provider = services.BuildServiceProvider();

        ITenantIdResolver resolver = provider.GetRequiredService<ITenantIdResolver>();
        Assert.IsType<CustomResolver>(resolver);
    }
}
