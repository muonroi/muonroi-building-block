namespace Muonroi.BuildingBlock.Test;

public class PermissionProviderExtensionsTests
{
    private class SampleProvider : IPermissionProvider
    {
        public IEnumerable<PermissionDefinition> GetPermissions()
        {
            return [];
        }
    }

    [Fact]
    public void AddPermissionProviders_Registers_Providers()
    {
        ServiceCollection services = [];
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        IPermissionProvider[] p = [.. provider.GetServices<IPermissionProvider>()];
        Assert.Contains(p, provider => provider is SampleProvider);
    }

    [Fact]
    public void AddPermissionProviders_Null_Services_Returns_Null()
    {
        IServiceCollection? services = null;
        IServiceCollection result = services!.AddPermissionProviders();
        Assert.Null(result);
    }

    [Fact]
    public void AddPermissionProviders_Can_Be_Called_Twice()
    {
        ServiceCollection services = [];
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        services.AddPermissionProviders(typeof(SampleProvider).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        int count = provider.GetServices<IPermissionProvider>().Count(p => p is SampleProvider);
        Assert.Equal(2, count);
    }
}
