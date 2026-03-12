namespace Muonroi.Grpc.Tests;

public class GrpcServicesConfigTests
{
    [Fact]
    public void Services_Property_Returns_Set_Value()
    {
        GrpcServiceConfig service = new()
        {
            Uri = "http://a"
        };
        Dictionary<string, GrpcServiceConfig> services = new()
        {
            ["a"] = service
        };
        GrpcServicesConfig configs = new()
        {
            Services = services
        };

        Assert.Same(services, configs.Services);
    }

    [Fact]
    public void Services_Property_Null_Or_Empty()
    {
        GrpcServicesConfig configs = new();

        Assert.NotNull(configs.Services);
        Assert.Empty(configs.Services);

        configs.Services = null!;
        Assert.Null(configs.Services);
    }

    [Fact]
    public void Uri_Property_Returns_Set_Value()
    {
        GrpcServiceConfig configs = new()
        {
            Uri = "http://x"
        };

        Assert.Equal("http://x", configs.Uri);
    }

    [Fact]
    public void Uri_Property_Defaults_To_Empty()
    {
        GrpcServiceConfig configs = new();

        Assert.Equal(string.Empty, configs.Uri);

        configs.Uri = null!;
        Assert.Null(configs.Uri);
    }

    [Fact]
    public void ClientDefaults_ForwardTenantId_Defaults_To_True()
    {
        GrpcClientDefaultsConfig configs = new();

        Assert.True(configs.ForwardTenantId);
        Assert.False(configs.ForwardAuthToken);
    }

    [Fact]
    public void ServiceConfig_Forwarding_Flags_Can_Be_Overridden()
    {
        GrpcServiceConfig configs = new()
        {
            ForwardAuthToken = true,
            ForwardTenantId = false
        };

        Assert.True(configs.ForwardAuthToken);
        Assert.False(configs.ForwardTenantId);
    }
}
