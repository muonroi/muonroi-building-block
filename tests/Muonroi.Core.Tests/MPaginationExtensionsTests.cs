namespace Muonroi.Core.Tests;

public class MPaginationExtensionsTests
{
    [Fact]
    public void AddPaginationConfigs_Binds_And_Adjusts_Config()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaginationConfigs:DefaultPageIndex"] = "0",
                ["PaginationConfigs:DefaultPageSize"] = "0",
                ["PaginationConfigs:MaxPageSize"] = "-1"
            })
            .Build();
        ServiceCollection services = [];
        MPaginationConfig config = new();

        services.AddPaginationConfigs(configuration, config);
        using ServiceProvider provider = services.BuildServiceProvider();
        MPaginationConfig result = provider.GetRequiredService<MPaginationConfig>();

        result.DefaultPageIndex.Should().Be(1);
        result.DefaultPageSize.Should().Be(15);
        result.MaxPageSize.Should().Be(15);
    }

    [Fact]
    public void AddPaginationConfigs_Allows_Multiple_Registrations()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        ServiceCollection services = [];

        services.AddPaginationConfigs(configuration, new MPaginationConfig());
        services.AddPaginationConfigs(configuration, new MPaginationConfig());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<MPaginationConfig>().Should().HaveCount(2);
    }
}
