namespace Muonroi.AspNetCore.Tests.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddConfigureHttpJson_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddConfigureHttpJson());
    }

    [Fact]
    public void AddConfigureHttpJson_RegistersJsonOptions()
    {
        var services = new ServiceCollection();
        var result = services.AddConfigureHttpJson();

        result.Should().BeSameAs(services);
        services.Should().Contain(d => d.ServiceType.Name.Contains("ConfigureOptions"));
    }

    [Fact]
    public void AddMediator_NullAssemblies_ReturnsServices()
    {
        var services = new ServiceCollection();
        var result = services.AddMediator(null);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddMediator_EmptyAssemblies_ReturnsServices()
    {
        var services = new ServiceCollection();
        var result = services.AddMediator([]);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void SwaggerConfig_RegistersSwaggerGen()
    {
        var services = new ServiceCollection();
        var result = services.SwaggerConfig("TestService");

        result.Should().BeSameAs(services);
        services.Should().Contain(d => d.ServiceType == typeof(IConfigureOptions<SwaggerGenOptions>));
    }
}
