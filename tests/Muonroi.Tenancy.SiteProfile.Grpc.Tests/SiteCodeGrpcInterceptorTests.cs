namespace Muonroi.Tenancy.SiteProfile.Grpc.Tests;

public sealed class SiteCodeGrpcInterceptorTests
{
    private static (ServerCallContext context, SiteCodeHolder holder, DefaultHttpContext httpContext)
        CreateMockContext(Metadata? requestHeaders = null, Dictionary<string, string>? httpHeaders = null)
    {
        var httpContext = new DefaultHttpContext();
        var holder = new SiteCodeHolder();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<ISiteCodeHolder>(holder)
            .BuildServiceProvider();

        if (httpHeaders is not null)
        {
            foreach (var kv in httpHeaders)
            {
                httpContext.Request.Headers[kv.Key] = kv.Value;
            }
        }

        var serverCallContext = Substitute.For<ServerCallContext>();
        serverCallContext.RequestHeaders.Returns(requestHeaders ?? new Metadata());

        // ServerCallContext.GetHttpContext() uses UserState["__HttpContext"]
        var userState = new Dictionary<object, object> { ["__HttpContext"] = httpContext };
        serverCallContext.UserState.Returns(userState);

        return (serverCallContext, holder, httpContext);
    }

    [Fact]
    public async Task MetadataExtraction_SetsSiteCodeHolder()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode" };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, holder, _) = CreateMockContext(new Metadata { new Metadata.Entry("SiteCode", "TCI") });

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        holder.SiteCode.Should().Be("TCI");
    }

    [Fact]
    public async Task MetadataExtraction_CaseInsensitive()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode" };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, holder, _) = CreateMockContext(new Metadata { new Metadata.Entry("sitecode", "TCI") });

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        holder.SiteCode.Should().Be("TCI");
    }

    [Fact]
    public async Task HttpHeaderFallback_WhenMetadataEmpty()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode", HttpHeaderFallbackKey = "x-site-code" };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, holder, _) = CreateMockContext(
            new Metadata(),
            new Dictionary<string, string> { ["x-site-code"] = "TCI" });

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        holder.SiteCode.Should().Be("TCI");
    }

    [Fact]
    public async Task HttpHeaderFallback_Disabled_WhenKeyNull()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode", HttpHeaderFallbackKey = null, Required = false };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, holder, _) = CreateMockContext(
            new Metadata(),
            new Dictionary<string, string> { ["x-site-code"] = "TCI" });

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        holder.SiteCode.Should().BeNull();
    }

    [Fact]
    public async Task Required_True_MissingSiteCode_ThrowsRpcException()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode", Required = true };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, _, _) = CreateMockContext(new Metadata());

        var act = () => interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task Required_False_MissingSiteCode_ContinuesSilently()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode", Required = false };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, holder, _) = CreateMockContext(new Metadata());

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        holder.SiteCode.Should().BeNull();
    }

    [Fact]
    public async Task SetsHttpContextItems()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode" };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));
        var (ctx, _, httpContext) = CreateMockContext(new Metadata { new Metadata.Entry("SiteCode", "TCI") });

        await interceptor.UnaryServerHandler("test", ctx, (req, c) => Task.FromResult("ok"));

        httpContext.Items[SiteCodeGrpcInterceptor.HttpContextItemKey].Should().Be("TCI");
    }

    [Fact]
    public async Task AllHandlerTypes_InvokeResolveSiteCode()
    {
        var options = new SiteGrpcOptions { MetadataKey = "SiteCode" };
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(options));

        // Unary handled above.
        
        // Server Streaming
        var (ssCtx, ssHolder, _) = CreateMockContext(new Metadata { new Metadata.Entry("SiteCode", "SS") });
        await interceptor.ServerStreamingServerHandler("test", Substitute.For<IServerStreamWriter<string>>(), ssCtx, (req, sw, c) => Task.CompletedTask);
        ssHolder.SiteCode.Should().Be("SS");

        // Client Streaming
        var (csCtx, csHolder, _) = CreateMockContext(new Metadata { new Metadata.Entry("SiteCode", "CS") });
        await interceptor.ClientStreamingServerHandler(Substitute.For<IAsyncStreamReader<string>>(), csCtx, (sr, c) => Task.FromResult("ok"));
        csHolder.SiteCode.Should().Be("CS");

        // Duplex Streaming
        var (dsCtx, dsHolder, _) = CreateMockContext(new Metadata { new Metadata.Entry("SiteCode", "DS") });
        await interceptor.DuplexStreamingServerHandler(Substitute.For<IAsyncStreamReader<string>>(), Substitute.For<IServerStreamWriter<string>>(), dsCtx, (sr, sw, c) => Task.CompletedTask);
        dsHolder.SiteCode.Should().Be("DS");
    }

    [Fact]
    public async Task ConcurrentCalls_DifferentSiteCodes_Isolated()
    {
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(new SiteGrpcOptions()));
        var siteCodes = new[] { "TCI", "FCD", "DEFAULT" };

        var tasks = siteCodes.Select(async siteCode =>
        {
            var (ctx, holder, _) = CreateMockContext(
                requestHeaders: new Metadata { new Metadata.Entry("SiteCode", siteCode) });

            await interceptor.UnaryServerHandler(
                "test", ctx, (req, c) => Task.FromResult("ok"));

            return (Expected: siteCode, Actual: holder.SiteCode);
        });

        var results = await Task.WhenAll(tasks);
        foreach (var (expected, actual) in results)
        {
            actual.Should().Be(expected);
        }
    }

    [Fact]
    public async Task ConcurrentCalls_VerifyNoSharedState()
    {
        var interceptor = new SiteCodeGrpcInterceptor(Options.Create(new SiteGrpcOptions()));
        var random = new Random();
        var iterations = 10;

        var tasks = Enumerable.Range(0, iterations).Select(async i =>
        {
            var siteCode = $"SITE_{i}_{random.Next(1000)}";
            var (ctx, holder, _) = CreateMockContext(
                requestHeaders: new Metadata { new Metadata.Entry("SiteCode", siteCode) });

            // Introduce some overlap
            await Task.Delay(random.Next(10, 50));

            await interceptor.UnaryServerHandler(
                "test", ctx, async (req, c) =>
                {
                    await Task.Delay(random.Next(10, 50));
                    return "ok";
                });

            return (Expected: siteCode, Actual: holder.SiteCode);
        });

        var results = await Task.WhenAll(tasks);
        foreach (var (expected, actual) in results)
        {
            actual.Should().Be(expected);
        }
    }
}
