namespace Muonroi.Tenancy.SiteProfile.Grpc.Tests;

public sealed class SiteGrpcDispatchHelperTests
{
    public abstract class FakeServiceBase
    {
        public abstract Task<string> DoWork(ServerCallContext ctx);
    }

    public class FakeTciService : FakeServiceBase
    {
        public override Task<string> DoWork(ServerCallContext ctx) => Task.FromResult("TCI-WORK");
    }

    public class FakeDefaultService : FakeServiceBase
    {
        public override Task<string> DoWork(ServerCallContext ctx) => Task.FromResult("DEFAULT-WORK");
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly SiteCodeHolder _siteCodeHolder;
    private readonly SiteGrpcDispatchHelper<FakeServiceBase> _helper;

    public SiteGrpcDispatchHelperTests()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<FakeServiceBase, FakeTciService>("TCI");
        services.AddKeyedScoped<FakeServiceBase, FakeDefaultService>("default");
        _serviceProvider = services.BuildServiceProvider();

        _siteCodeHolder = new SiteCodeHolder();
        var log = Substitute.For<IMLog<SiteGrpcDispatchHelper<FakeServiceBase>>>();
        _helper = new SiteGrpcDispatchHelper<FakeServiceBase>(_serviceProvider, _siteCodeHolder, log);
    }

    [Fact]
    public async Task ExactSiteMatch_DispatchesToCorrectHandler()
    {
        _siteCodeHolder.SiteCode = "TCI";
        var context = Substitute.For<ServerCallContext>();

        var result = await _helper.DispatchAsync(context, (h, ctx) => h.DoWork(ctx));

        result.Should().Be("TCI-WORK");
    }

    [Fact]
    public async Task FallbackToDefault_WhenSiteNotRegistered()
    {
        _siteCodeHolder.SiteCode = "UNKNOWN";
        var context = Substitute.For<ServerCallContext>();

        var result = await _helper.DispatchAsync(context, (h, ctx) => h.DoWork(ctx));

        result.Should().Be("DEFAULT-WORK");
    }

    [Fact]
    public async Task NoHandler_ThrowsRpcException()
    {
        var services = new ServiceCollection();
        // No registrations
        var sp = services.BuildServiceProvider();
        var helper = new SiteGrpcDispatchHelper<FakeServiceBase>(sp, _siteCodeHolder, Substitute.For<IMLog<SiteGrpcDispatchHelper<FakeServiceBase>>>());
        
        _siteCodeHolder.SiteCode = "TCI";
        var context = Substitute.For<ServerCallContext>();

        var act = () => helper.DispatchAsync(context, (h, ctx) => h.DoWork(ctx));

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.Internal);
    }

    [Fact]
    public async Task NullSiteCode_DefaultsToDefaultKey()
    {
        _siteCodeHolder.SiteCode = null;
        var context = Substitute.For<ServerCallContext>();

        var result = await _helper.DispatchAsync(context, (h, ctx) => h.DoWork(ctx));

        result.Should().Be("DEFAULT-WORK");
    }

    [Fact]
    public async Task DispatchAsync_PassesContextToHandler()
    {
        _siteCodeHolder.SiteCode = "TCI";
        var context = Substitute.For<ServerCallContext>();
        
        var receivedContext = (ServerCallContext)null!;
        await _helper.DispatchAsync(context, (h, ctx) =>
        {
            receivedContext = ctx;
            return h.DoWork(ctx);
        });

        receivedContext.Should().BeSameAs(context);
    }
}
