namespace Muonroi.AspNetCore.Tests.Extensions;

public class ArchitectureValidationExtensionsTests
{
    [Fact]
    public void EnforceArchitecture_WithTestAssembly_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var assembly = typeof(ArchitectureValidationExtensionsTests).Assembly;

        var act = () => services.EnforceArchitecture(assembly, throwOnViolation: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceArchitecture_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var assembly = typeof(ArchitectureValidationExtensionsTests).Assembly;

        var result = services.EnforceArchitecture(assembly, throwOnViolation: false);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void EnforceArchitecture_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var assembly = typeof(ArchitectureValidationExtensionsTests).Assembly;

        services.EnforceArchitecture(assembly);

        services.Should().Contain(d => d.ServiceType == typeof(IHostedService));
    }
}
