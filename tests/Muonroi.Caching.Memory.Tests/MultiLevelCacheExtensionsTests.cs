namespace Muonroi.Caching.Memory.Tests;

public class MultiLevelCacheExtensionsTests
{
    [Fact]
    public void AddMultiLevelCaching_Registers_Required_Services()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        ServiceCollection services = [];

        services.AddMultiLevelCaching(config);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IMultiLevelCacheService>().Should().NotBeNull();
        provider.GetService<IMemoryCache>().Should().NotBeNull();
        provider.GetService<IDistributedCache>().Should().NotBeNull();
    }

    [Fact]
    public void AddMultiLevelCaching_Can_Be_Called_Multiple_Times()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        ServiceCollection services = [];

        services.AddMultiLevelCaching(config);
        services.AddMultiLevelCaching(config);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IMultiLevelCacheService>().Should().NotBeNull();
        services.Count(x => x.ServiceType == typeof(IMultiLevelCacheService)).Should().Be(2);
    }

    [Fact]
    public void AddMultiLevelCaching_Null_Configuration_CurrentImplementation_Allows()
    {
        ServiceCollection services = [];

        Action action = () => services.AddMultiLevelCaching(null!);

        action.Should().NotThrow();
    }

    [Fact]
    public void AddMultiLevelCaching_Null_Services_Throws()
    {
        ServiceCollection? services = null;
        IConfiguration config = new ConfigurationBuilder().Build();

        Action action = () => MultiLevelCacheExtensions.AddMultiLevelCaching(services!, config);

        action.Should().Throw<ArgumentNullException>();
    }
}
