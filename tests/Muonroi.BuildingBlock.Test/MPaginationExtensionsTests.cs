namespace Muonroi.BuildingBlock.Test;

public class MPaginationExtensionsTests
{
    [Fact]
    public void AddPaginationConfigs_Binds_And_Adjusts_Config()
    {
        Dictionary<string, string?> dict = new()
        {
            ["PaginationConfigs:DefaultPageIndex"] = "0",
            ["PaginationConfigs:DefaultPageSize"] = "0",
            ["PaginationConfigs:MaxPageSize"] = "-1"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        ServiceCollection services = [];
        MPaginationConfig config = new();
        services.AddPaginationConfigs(configuration, config);
        ServiceProvider provider = services.BuildServiceProvider();
        MPaginationConfig result = provider.GetRequiredService<MPaginationConfig>();
        Assert.Equal(1, result.DefaultPageIndex);
        Assert.Equal(15, result.DefaultPageSize);
        Assert.Equal(15, result.MaxPageSize);
    }

    [Fact]
    public void AddPaginationConfigs_Null_Config_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        ServiceCollection services = [];
        Assert.Throws<NullReferenceException>(() =>
            services.AddPaginationConfigs(configuration, (MPaginationConfig)null!));
    }

    [Fact]
    public void AddPaginationConfigs_Allows_Multiple_Registrations()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        ServiceCollection services = [];
        MPaginationConfig c1 = new();
        MPaginationConfig c2 = new();
        services.AddPaginationConfigs(configuration, c1);
        services.AddPaginationConfigs(configuration, c2);
        ServiceProvider provider = services.BuildServiceProvider();
        IList<MPaginationConfig> list = [.. provider.GetServices<MPaginationConfig>()];
        Assert.Contains(c1, list);
        Assert.Contains(c2, list);
    }
}
