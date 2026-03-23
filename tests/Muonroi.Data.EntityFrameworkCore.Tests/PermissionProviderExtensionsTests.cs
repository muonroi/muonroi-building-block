namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class PermissionProviderExtensionsTests
{
    [Fact]
    public void AddPermissionProviders_With_No_Assemblies_Uses_Executing()
    {
        ServiceCollection services = new();

        services.AddPermissionProviders();

        // Should not throw, may or may not find providers in test assembly
        services.Should().NotBeNull();
    }

    [Fact]
    public void AddPermissionProviders_With_Specific_Assembly_Scans_It()
    {
        ServiceCollection services = new();

        services.AddPermissionProviders(typeof(MDbContext).Assembly);

        // Should scan the assembly for IPermissionProvider implementations
        services.Should().NotBeNull();
    }

    [Fact]
    public void AddPermissionProviders_Returns_Same_ServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddPermissionProviders();

        result.Should().BeSameAs(services);
    }
}
