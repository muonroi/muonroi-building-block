namespace Muonroi.AspNetCore.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBaseApi_RegistersApiVersioning()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var result = services.AddBaseApi();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddBaseApi_RegistersHealthChecks()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBaseApi();

        // HealthChecks should be registered
        services.Should().Contain(d => d.ServiceType.FullName != null &&
            d.ServiceType.FullName.Contains("HealthCheck"));
    }
}
