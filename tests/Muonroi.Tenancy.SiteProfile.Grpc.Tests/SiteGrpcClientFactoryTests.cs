namespace Muonroi.Tenancy.SiteProfile.Grpc.Tests;

public sealed class SiteGrpcClientFactoryTests
{
    private interface IFakeFacade { }
    private class FakeFacadeImpl : IFakeFacade { }

    private abstract class FakeClient : ClientBase
    {
        protected FakeClient() : base() { }
        protected FakeClient(CallInvoker callInvoker) : base(callInvoker) { }
    }

    private class ConcreteFakeClient : FakeClient
    {
        public ConcreteFakeClient() : base() { }
        public ConcreteFakeClient(CallInvoker callInvoker) : base(callInvoker) { }
    }

    private readonly ISiteProfileResolver _resolver;
    private readonly SiteGrpcClientRegistry _registry;
    private readonly GrpcClientFactoryAccessor _accessor;
    private readonly IServiceCollection _services;
    private SiteGrpcClientFactory _factory = null!;

    public SiteGrpcClientFactoryTests()
    {
        _resolver = Substitute.For<ISiteProfileResolver>();
        _registry = new SiteGrpcClientRegistry();
        _accessor = new GrpcClientFactoryAccessor();
        _services = new ServiceCollection();
    }

    private void BuildFactory()
    {
        _factory = new SiteGrpcClientFactory(_resolver, _registry, _accessor, _services.BuildServiceProvider());
    }

    [Fact]
    public void CreateForCurrentSite_ResolvesCorrectDescriptor_ButCreateClientReturnsNull_ThrowsInvalidOperationException()
    {
        _resolver.Current.SiteId.Returns("TCI");
        var descriptor = new SiteGrpcClientDescriptor("TCI", "test-svc", typeof(ConcreteFakeClient));
        _registry.Add(descriptor);
        BuildFactory();

        var act = () => _factory.CreateForCurrentSite<ConcreteFakeClient>("test-svc");

        act.Should().Throw<MInternalException>()
            .WithMessage("*No gRPC client cached*");
    }

    [Fact]
    public void CreateForCurrentSite_FallsBackToDefault_ButCreateClientReturnsNull_ThrowsInvalidOperationException()
    {
        _resolver.Current.SiteId.Returns("UNKNOWN");
        var descriptor = new SiteGrpcClientDescriptor("default", "test-svc", typeof(ConcreteFakeClient));
        _registry.Add(descriptor);
        BuildFactory();

        var act = () => _factory.CreateForCurrentSite<ConcreteFakeClient>("test-svc");

        act.Should().Throw<MInternalException>()
            .WithMessage("*No gRPC client cached*");
    }

    [Fact]
    public void CreateForCurrentSite_UnknownSite_NoDefault_ThrowsInvalidOperationException()
    {
        _resolver.Current.SiteId.Returns("TCI");
        BuildFactory();

        var act = () => _factory.CreateForCurrentSite<ConcreteFakeClient>("test-svc");

        act.Should().Throw<MInternalException>()
            .WithMessage("*No gRPC client registered for site 'TCI'*");
    }

    [Fact]
    public void CreateForCurrentSite_NullServiceName_ThrowsArgumentException()
    {
        BuildFactory();
        var act = () => _factory.CreateForCurrentSite<ConcreteFakeClient>("");

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void CreateFacadeForCurrentSite_ResolvesKeyedFacade()
    {
        _resolver.Current.SiteId.Returns("TCI");
        var expectedFacade = new FakeFacadeImpl();
        _services.AddKeyedSingleton<IFakeFacade>("facade:test-svc:TCI", expectedFacade);
        BuildFactory();

        var result = _factory.CreateFacadeForCurrentSite<IFakeFacade>("test-svc");

        result.Should().BeSameAs(expectedFacade);
    }

    [Fact]
    public void CreateFacadeForCurrentSite_FallsBackToDefault()
    {
        _resolver.Current.SiteId.Returns("UNKNOWN");
        var expectedFacade = new FakeFacadeImpl();
        _services.AddKeyedSingleton<IFakeFacade>("facade:test-svc:default", expectedFacade);
        BuildFactory();

        var result = _factory.CreateFacadeForCurrentSite<IFakeFacade>("test-svc");

        result.Should().BeSameAs(expectedFacade);
    }

    [Fact]
    public void CreateFacadeForCurrentSite_NoFacade_ThrowsInvalidOperationException()
    {
        _resolver.Current.SiteId.Returns("TCI");
        BuildFactory();

        var act = () => _factory.CreateFacadeForCurrentSite<IFakeFacade>("test-svc");

        act.Should().Throw<MInternalException>()
            .WithMessage("*No gRPC facade registered for site 'TCI'*");
    }
}
