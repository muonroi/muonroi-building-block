namespace Muonroi.BuildingBlock.Test;

public class CorsExtensionsTests
{
    [Fact]
    public void AddCors_Adds_Policy()
    {
        Dictionary<string, string?> data = new()
        {
            ["MAllowDomains"] = "http://a.com,http://b.com"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        BuildingBlock.External.Cors.CorsExtensions.AddCors(services, config);
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CorsOptions> opts = provider.GetRequiredService<IOptions<CorsOptions>>();
        CorsPolicy? policy = opts.Value.GetPolicy("MAllowDomains");
        Assert.NotNull(policy);
        Assert.Equal(2, policy!.Origins.Count);
    }

    [Fact]
    public void AddCors_Null_Config_Throws()
    {
        ServiceCollection services = [];
        Assert.Throws<NullReferenceException>(() => BuildingBlock.External.Cors.CorsExtensions.AddCors(services, null!));
    }

    [Fact]
    public void AddCors_Duplicate_Calls_Add_Multiple_Policies()
    {
        Dictionary<string, string?> data = new()
        {
            ["MAllowDomains"] = "http://a.com"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        BuildingBlock.External.Cors.CorsExtensions.AddCors(services, config);
        BuildingBlock.External.Cors.CorsExtensions.AddCors(services, config);
        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CorsOptions> opts = provider.GetRequiredService<IOptions<CorsOptions>>();
        CorsPolicy? policy = opts.Value.GetPolicy("MAllowDomains");
        Assert.NotNull(policy);
    }
}
