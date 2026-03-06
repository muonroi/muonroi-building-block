using Muonroi.Governance;

namespace Muonroi.BuildingBlock.Test;

public class PermissionFilterPdpModeTests
{
    [Fact]
    public async Task PdpAuthoritativeAllow_BypassesLocalBitmask()
    {
        PermissionFilter<TestPerm> filter = new(NullLogger<PermissionFilter<TestPerm>>.Instance);
        ServiceProvider sp = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(new StubPdpService(MPolicyDecisionResult.Allowed("pdp.opa")))
            .BuildServiceProvider();

        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
            RequestServices = sp
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)),
            "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        bool called = false;
        await filter.OnActionExecutionAsync(exc, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        });

        Assert.True(called);
    }

    [Fact]
    public async Task PdpAuthoritativeDeny_BlocksEvenWhenLocalBitmaskAllows()
    {
        PermissionFilter<TestPerm> filter = new(NullLogger<PermissionFilter<TestPerm>>.Instance);
        ServiceProvider sp = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(new StubPdpService(MPolicyDecisionResult.Denied("pdp.opa")))
            .BuildServiceProvider();

        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id),
            RequestServices = sp
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)),
            "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            filter.OnActionExecutionAsync(exc, () => Task.FromResult(new ActionExecutedContext(ac, [], new object()))));
    }

    [Fact]
    public async Task PdpFallback_UsesLocalBitmaskAuthorization()
    {
        PermissionFilter<TestPerm> filter = new(NullLogger<PermissionFilter<TestPerm>>.Instance);
        ServiceProvider sp = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(
                new StubPdpService(MPolicyDecisionResult.LocalFallback("local.fallback")))
            .BuildServiceProvider();

        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id),
            RequestServices = sp
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)),
            "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        bool called = false;
        await filter.OnActionExecutionAsync(exc, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        });

        Assert.True(called);
    }

    private sealed class StubPdpService(MPolicyDecisionResult result) : IMPolicyDecisionService
    {
        public bool IsEnabled => true;

        public Task<MPolicyDecisionResult> EvaluateAsync(MPolicyDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
