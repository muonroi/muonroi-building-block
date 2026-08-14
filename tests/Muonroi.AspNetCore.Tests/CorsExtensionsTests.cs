namespace Muonroi.AspNetCore.Tests;

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

        services.AddCors(config);

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CorsOptions> options = provider.GetRequiredService<IOptions<CorsOptions>>();
        CorsPolicy? policy = options.Value.GetPolicy("MAllowDomains");

        Assert.NotNull(policy);
        Assert.Equal(2, policy!.Origins.Count);
    }

    [Fact]
    public void AddCors_Null_Config_Throws()
    {
        ServiceCollection services = [];
        Assert.Throws<ArgumentNullException>(() => services.AddCors(null!));
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

        services.AddCors(config);
        services.AddCors(config);

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CorsOptions> options = provider.GetRequiredService<IOptions<CorsOptions>>();
        CorsPolicy? policy = options.Value.GetPolicy("MAllowDomains");
        Assert.NotNull(policy);
    }
}
