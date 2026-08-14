namespace Muonroi.SignalR.Tests;

public sealed class TenantHubFilterTests
{
    [Fact]
    public async Task InvokeMethodAsync_WhenMultiTenantEnabled_AndTenantMissing_ShouldThrow()
    {
        ITenantIdResolver resolver = Substitute.For<ITenantIdResolver>();
        resolver.ResolveTenantIdAsync(Arg.Any<HttpContext>()).Returns((string?)null);
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        TenantHubFilter filter = new(resolver, new MTokenInfo { MultiTenantEnabled = true }, guard);
        HubInvocationContext invocation = CreateInvocationContext(httpContext: new DefaultHttpContext());

        Func<Task> act = async () => await filter.InvokeMethodAsync(invocation, _ => new ValueTask<object?>("ok"));

        await act.Should().ThrowAsync<HubException>().WithMessage("Tenant ID is required.");
        guard.Received(1).EnsureFeature(FreeTierFeatures.Premium.MultiTenant);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenTenantClaimDiffers_ShouldThrow()
    {
        ITenantIdResolver resolver = Substitute.For<ITenantIdResolver>();
        resolver.ResolveTenantIdAsync(Arg.Any<HttpContext>()).Returns("tenant-a");
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-b")
        ], "test"));
        TenantHubFilter filter = new(resolver, new MTokenInfo { MultiTenantEnabled = true }, guard);
        HubInvocationContext invocation = CreateInvocationContext(httpContext);

        Func<Task> act = async () => await filter.InvokeMethodAsync(invocation, _ => new ValueTask<object?>("ok"));

        await act.Should().ThrowAsync<HubException>().WithMessage("Tenant mismatch.");
    }

    [Fact]
    public async Task InvokeMethodAsync_Sets_And_Clears_TenantContext_Around_Next()
    {
        ITenantIdResolver resolver = Substitute.For<ITenantIdResolver>();
        resolver.ResolveTenantIdAsync(Arg.Any<HttpContext>()).Returns("tenant-a");
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        TenantHubFilter filter = new(resolver, new MTokenInfo { MultiTenantEnabled = false }, guard);
        HubInvocationContext invocation = CreateInvocationContext(new DefaultHttpContext());
        string? tenantSeenInside = null;

        object? result = await filter.InvokeMethodAsync(invocation, _ =>
        {
            tenantSeenInside = TenantContext.CurrentTenantId;
            return new ValueTask<object?>("done");
        });

        result.Should().Be("done");
        tenantSeenInside.Should().Be("tenant-a");
        TenantContext.CurrentTenantId.Should().BeNull();
        guard.DidNotReceive().EnsureFeature(Arg.Any<string>());
    }

    private static HubInvocationContext CreateInvocationContext(HttpContext? httpContext)
    {
        TestHubCallerContext callerContext = new(httpContext);
        return new HubInvocationContext(
            callerContext,
            new ServiceCollection().BuildServiceProvider(),
            new TestHub(),
            DummyMethod,
            Array.Empty<object>());
    }

    private static readonly MethodInfo DummyMethod =
        typeof(TestHub).GetMethod(nameof(TestHub.Ping), BindingFlags.Public | BindingFlags.Instance)!;

    private sealed class TestHub : Hub
    {
        public Task Ping()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly IFeatureCollection _features = new FeatureCollection();

        public TestHubCallerContext(HttpContext? httpContext)
        {
            if (httpContext is not null)
            {
                _features.Set<IHttpContextFeature>(new TestHttpContextFeature { HttpContext = httpContext });
                User = httpContext.User;
            }
        }

        public override string ConnectionId => "conn-1";
        public override string? UserIdentifier => User?.Identity?.Name;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => _features;
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
