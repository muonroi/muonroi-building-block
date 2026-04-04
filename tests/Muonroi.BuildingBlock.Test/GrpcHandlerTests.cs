using Grpc.AspNetCore.Server;
using Microsoft.Extensions.Options;
using ServerGrpcServiceOptions = Grpc.AspNetCore.Server.GrpcServiceOptions;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class GrpcHandlerTests
{
    private class DummyGrpcClient : ClientBase<DummyGrpcClient>
    {
        public DummyGrpcClient(CallInvoker callInvoker) : base(callInvoker)
        {
        }

        protected DummyGrpcClient()
        {
        }

        protected DummyGrpcClient(ClientBaseConfiguration conf) : base(conf)
        {
        }

        protected override DummyGrpcClient NewInstance(ClientBaseConfiguration conf)
        {
            return new DummyGrpcClient(conf);
        }
    }

    private class AnotherGrpcClient : ClientBase<AnotherGrpcClient>
    {
        public AnotherGrpcClient(CallInvoker callInvoker) : base(callInvoker)
        {
        }

        protected AnotherGrpcClient()
        {
        }

        protected AnotherGrpcClient(ClientBaseConfiguration conf) : base(conf)
        {
        }

        protected override AnotherGrpcClient NewInstance(ClientBaseConfiguration conf)
        {
            return new AnotherGrpcClient(conf);
        }
    }

    [Fact]
    public void AddGrpcServer_Registers_Interceptor()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<GrpcServerInterceptor>());
    }

    [Fact]
    public void AddGrpcServer_Registers_Grpc_ServiceOptions()
    {
        ServiceCollection services = [];

        services.AddGrpcServer();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ServerGrpcServiceOptions> options = provider.GetRequiredService<IOptions<ServerGrpcServiceOptions>>();

        Assert.NotNull(options.Value);
    }

    [Fact]
    public void AddGrpcServer_Applies_Configured_Server_Options()
    {
        ServiceCollection services = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServicesConfig:Server:MaxSendMessageSizeBytes"] = "2048",
                ["GrpcServicesConfig:Server:MaxReceiveMessageSizeBytes"] = "4096",
                ["GrpcServicesConfig:Server:EnableDetailedErrors"] = "false"
            })
            .Build();

        services.AddGrpcServer(config);

        ServiceProvider provider = services.BuildServiceProvider();
        ServerGrpcServiceOptions options = provider.GetRequiredService<IOptions<ServerGrpcServiceOptions>>().Value;

        Assert.Equal(2048, options.MaxSendMessageSize);
        Assert.Equal(4096, options.MaxReceiveMessageSize);
        Assert.False(options.EnableDetailedErrors);
    }

    [Fact]
    public void AddGrpcServer_Null_Services_Throws()
    {
        Assert.Throws<MArgumentException>(() => GrpcHandler.AddGrpcServer(null!));
    }

    [Fact]
    public void AddGrpcClient_Adds_Client()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        GrpcHandler.AddGrpcClient<DummyGrpcClient>(services, "http://localhost");
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<DummyGrpcClient>());
    }

    [Fact]
    public void AddGrpcClient_Invalid_Uri_Throws()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        Assert.Throws<UriFormatException>(() => GrpcHandler.AddGrpcClient<DummyGrpcClient>(services, "ht tp:/bad"));
    }

    [Fact]
    public void AddGrpcClient_Null_ServiceCollection_Throws()
    {
        Assert.Throws<MArgumentException>(() =>
            GrpcHandler.AddGrpcClient<DummyGrpcClient>(null!, "http://localhost"));
    }

    [Fact]
    public void AddGrpcClients_Adds_Multiple()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServicesConfig:Services:ClientA:Uri"] = "http://localhost",
                ["GrpcServicesConfig:Services:ClientB:Uri"] = "http://localhost"
            })
            .Build();
        Dictionary<string, Type> clients = new()
        {
            ["ClientA"] = typeof(DummyGrpcClient),
            ["ClientB"] = typeof(AnotherGrpcClient)
        };
        services.AddGrpcClients(config, clients);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<DummyGrpcClient>());
        Assert.NotNull(provider.GetService<AnotherGrpcClient>());
    }

    [Fact]
    public void AddGrpcClients_Null_List_Throws()
    {
        ServiceCollection services = [];
        IConfiguration config = new ConfigurationBuilder().Build();
        Assert.Throws<NullReferenceException>(() => services.AddGrpcClients(config, null!));
    }

    [Fact]
    public void AddGrpcClients_Binds_Legacy_GrpcServices_Section()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcServices:Services:ClientA:Uri"] = "http://localhost"
            })
            .Build();
        Dictionary<string, Type> clients = new()
        {
            ["ClientA"] = typeof(DummyGrpcClient)
        };

        services.AddGrpcClients(config, clients);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<DummyGrpcClient>());
    }

    [Fact]
    public void AddGrpcClient_ByType_Adds_Client()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        MethodInfo method = typeof(GrpcHandler).GetMethod("AddGrpcClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? builder = method.Invoke(null, [services, typeof(DummyGrpcClient), "http://localhost"]);
        Assert.NotNull(builder);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<DummyGrpcClient>());
    }

    [Fact]
    public void AddGrpcClient_ByType_InvalidUri_Throws()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton(new MAuthenticateInfoContext(false));
        services.AddGrpcServer();
        MethodInfo method = typeof(GrpcHandler).GetMethod("AddGrpcClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [services, typeof(DummyGrpcClient), "ht tp:/bad"]));
    }
}
