namespace Muonroi.BuildingBlock.Test;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBaseApi_Registers_Services()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddBaseApi();
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IApiVersionReader>());
        Assert.NotNull(provider.GetService<HealthCheckService>());
    }

    [Fact]
    public void AddBaseApi_Duplicate_Calls_Register_Duplicates()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddBaseApi();
        services.AddBaseApi();
        ServiceProvider provider = services.BuildServiceProvider();
        int count = provider.GetServices<IApiVersionReader>().Count();
        Assert.Equal(2, count);
    }

    [Fact]
    public void AddBaseApi_Null_Services_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddBaseApi());
    }
}
