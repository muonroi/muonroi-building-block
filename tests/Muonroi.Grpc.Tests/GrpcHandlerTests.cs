namespace Muonroi.Grpc.Tests;

using ServerGrpcServiceOptions = global::Grpc.AspNetCore.Server.GrpcServiceOptions;

public class GrpcHandlerTests
{
    private sealed class DummyGrpcClient : ClientBase<DummyGrpcClient>
    {
        public DummyGrpcClient(CallInvoker callInvoker) : base(callInvoker)
        {
        }

        protected DummyGrpcClient()
        {
        }

        protected DummyGrpcClient(ClientBaseConfiguration configuration) : base(configuration)
        {
        }

        protected override DummyGrpcClient NewInstance(ClientBaseConfiguration configuration)
        {
            return new DummyGrpcClient(configuration);
        }
    }

    private sealed class AnotherGrpcClient : ClientBase<AnotherGrpcClient>
    {
        public AnotherGrpcClient(CallInvoker callInvoker) : base(callInvoker)
        {
        }

        protected AnotherGrpcClient()
        {
        }

        protected AnotherGrpcClient(ClientBaseConfiguration configuration) : base(configuration)
        {
        }

        protected override AnotherGrpcClient NewInstance(ClientBaseConfiguration configuration)
        {
            return new AnotherGrpcClient(configuration);
        }
    }

    private static ServiceCollection CreateLicensedServices()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.Grpc]
        });
        return services;
    }

    [Fact]
    public void AddGrpcServer_Registers_Interceptor()
    {
        ServiceCollection services = CreateLicensedServices();

        services.AddGrpcServer();

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<GrpcServerInterceptor>().Should().NotBeNull();
    }

    [Fact]
    public void AddGrpcServer_Applies_Configured_Server_Options()
    {
        ServiceCollection services = CreateLicensedServices();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServicesConfig:Server:MaxSendMessageSizeBytes"] = "2048",
                ["GrpcServicesConfig:Server:MaxReceiveMessageSizeBytes"] = "4096",
                ["GrpcServicesConfig:Server:EnableDetailedErrors"] = "false"
            })
            .Build();

        services.AddGrpcServer(config);

        ServerGrpcServiceOptions options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ServerGrpcServiceOptions>>()
            .Value;

        options.MaxSendMessageSize.Should().Be(2048);
        options.MaxReceiveMessageSize.Should().Be(4096);
        options.EnableDetailedErrors.Should().BeFalse();
    }

    [Fact]
    public void AddGrpcClient_Adds_Client()
    {
        ServiceCollection services = CreateLicensedServices();
        services.AddGrpcServer();

        GrpcHandler.AddGrpcClient<DummyGrpcClient>(services, "http://localhost");

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<DummyGrpcClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddGrpcClients_Adds_Multiple()
    {
        ServiceCollection services = CreateLicensedServices();
        services.AddGrpcServer();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServicesConfig:Services:ClientA:Uri"] = "http://localhost",
                ["GrpcServicesConfig:Services:ClientB:Uri"] = "http://localhost"
            })
            .Build();

        services.AddGrpcClients(configuration, new Dictionary<string, Type>
        {
            ["ClientA"] = typeof(DummyGrpcClient),
            ["ClientB"] = typeof(AnotherGrpcClient)
        });

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<DummyGrpcClient>().Should().NotBeNull();
        provider.GetService<AnotherGrpcClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddGrpcClients_Binds_Legacy_GrpcServices_Section()
    {
        ServiceCollection services = CreateLicensedServices();
        services.AddGrpcServer();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServices:Services:ClientA:Uri"] = "http://localhost"
            })
            .Build();

        services.AddGrpcClients(configuration, new Dictionary<string, Type>
        {
            ["ClientA"] = typeof(DummyGrpcClient)
        });

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<DummyGrpcClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddGrpcClient_ByType_InvalidUri_Throws()
    {
        ServiceCollection services = CreateLicensedServices();
        services.AddGrpcServer();

        MethodInfo method = typeof(GrpcHandler).GetMethod("AddGrpcClient", BindingFlags.NonPublic | BindingFlags.Static)!;

        Action action = () => method.Invoke(null, [services, typeof(DummyGrpcClient), "ht tp:/bad"]);

        action.Should().Throw<TargetInvocationException>();
    }
}

internal static class MetadataAssertions
{
    public static string? GetValue(this Metadata metadata, string key)
    {
        return metadata.FirstOrDefault(x => x.Key == key)?.Value;
    }
}
