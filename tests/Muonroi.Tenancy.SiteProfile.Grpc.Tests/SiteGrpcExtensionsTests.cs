using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Grpc.Tests;

public sealed class SiteGrpcExtensionsTests
{
    public class FakeBase { }
    public class FakeImpl : FakeBase { }

    public abstract class FakeClient : ClientBase
    {
        protected FakeClient() : base() { }
        protected FakeClient(CallInvoker callInvoker) : base(callInvoker) { }
    }

    public class ConcreteFakeClient : FakeClient
    {
        public ConcreteFakeClient() : base() { }
        public ConcreteFakeClient(CallInvoker callInvoker) : base(callInvoker) { }
    }

    [Fact]
    public void AddSiteGrpcServices_RegistersISiteCodeHolder()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcServices();
        var sp = services.BuildServiceProvider();

        var holder = sp.GetService<ISiteCodeHolder>();
        holder.Should().NotBeNull();
        holder.Should().BeOfType<SiteCodeHolder>();
    }

    [Fact]
    public void AddSiteGrpcServices_RegistersSiteCodeGrpcInterceptor()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcServices();
        var sp = services.BuildServiceProvider();

        var interceptor = sp.GetService<SiteCodeGrpcInterceptor>();
        interceptor.Should().NotBeNull();
    }

    [Fact]
    public void AddSiteGrpcServices_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcServices(o =>
        {
            o.MetadataKey = "x-site";
            o.Required = false;
        });
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<SiteGrpcOptions>>().Value;
        options.MetadataKey.Should().Be("x-site");
        options.Required.Should().BeFalse();
    }

    [Fact]
    public void AddSiteGrpcServices_DefaultOptions_MetadataKeyIsSiteCode()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcServices();
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<SiteGrpcOptions>>().Value;
        options.MetadataKey.Should().Be("SiteCode");
        options.Required.Should().BeTrue();
    }

    [Fact]
    public void AddSiteGrpcClient_RegistersDescriptor()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcClient<ConcreteFakeClient>("TCI", "test-svc");
        var sp = services.BuildServiceProvider();

        var descriptors = sp.GetServices<SiteGrpcClientDescriptor>();
        descriptors.Should().ContainSingle(d => 
            d.SiteId == "TCI" && 
            d.ServiceName == "test-svc" && 
            d.ClientType == typeof(ConcreteFakeClient));
    }

    [Fact]
    public void AddSiteGrpcHandler_RegistersKeyedService()
    {
        var services = new ServiceCollection();
        services.AddSiteGrpcHandler<FakeBase, FakeImpl>("TCI");
        var sp = services.BuildServiceProvider();

        var handler = sp.GetKeyedService<FakeBase>("TCI");
        handler.Should().NotBeNull();
        handler.Should().BeOfType<FakeImpl>();
    }

    [Fact]
    public void AddSiteGrpcClientFactory_RegistersRegistryAndFactory()
    {
        var services = new ServiceCollection();
        // SiteGrpcClientFactory needs ISiteProfileResolver
        services.AddSingleton(Substitute.For<ISiteProfileResolver>());
        services.AddSiteGrpcClientFactory();
        var sp = services.BuildServiceProvider();

        sp.GetService<SiteGrpcClientRegistry>().Should().NotBeNull();
        sp.GetService<ISiteGrpcClientFactory>().Should().NotBeNull();
        sp.GetService<GrpcClientFactoryAccessor>().Should().NotBeNull();
    }

    [Fact]
    public void AddSiteGrpcDispatcher_RegistersDispatchHelper()
    {
        var services = new ServiceCollection();
        // SiteGrpcDispatchHelper depends on IServiceProvider, ISiteCodeHolder, IMLog
        services.AddSingleton(Substitute.For<IMLog<SiteGrpcDispatchHelper<FakeBase>>>());
        services.AddSiteGrpcServices();
        services.AddSiteGrpcDispatcher<FakeBase>();
        var sp = services.BuildServiceProvider();

        sp.GetService<SiteGrpcDispatchHelper<FakeBase>>().Should().NotBeNull();
    }

    public interface IFakeFacade { }
    public class FakeFacadeImpl : IFakeFacade 
    {
        public ConcreteFakeClient Client { get; }
        public FakeFacadeImpl(ConcreteFakeClient client) => Client = client;
    }

    [Fact]
    public void AddSiteGrpcFacadeClient_RegistersKeyedService_AndResolvesWithDependencies()
    {
        var services = new ServiceCollection();
        var client = new ConcreteFakeClient();
        
        // Use real accessor as it is sealed and cannot be mocked by NSubstitute
        services.AddSingleton<GrpcClientFactoryAccessor>();
        services.AddSingleton(client); // Resolve via SP fallback
        services.AddSiteGrpcFacadeClient<IFakeFacade, FakeFacadeImpl>("TCI", "test-svc");
        var sp = services.BuildServiceProvider();

        var facade = sp.GetKeyedService<IFakeFacade>("facade:test-svc:TCI") as FakeFacadeImpl;
        
        facade.Should().NotBeNull();
        facade!.Client.Should().BeSameAs(client);
    }
}
