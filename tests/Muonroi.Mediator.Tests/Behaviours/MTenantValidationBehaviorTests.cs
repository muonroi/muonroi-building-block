namespace Muonroi.Mediator.Tests.Behaviours;

using MUnauthorizedException = Muonroi.Mediator.Exceptions.MUnauthorizedException;

public class MTenantValidationBehaviorTests
{
    // ── Request types ──────────────────────────────────────────────────────────

    private record TenantRequest(string Payload) : IMTenantRequest<string>;
    private record PlainRequest(string Payload) : IRequest<string>;

    private class TenantHandler : IRequestHandler<TenantRequest, string>
    {
        public Task<string> Handle(TenantRequest request, CancellationToken ct)
            => Task.FromResult($"handled:{request.Payload}");
    }

    private class PlainHandler : IRequestHandler<PlainRequest, string>
    {
        public Task<string> Handle(PlainRequest request, CancellationToken ct)
            => Task.FromResult($"plain:{request.Payload}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IMediator BuildMediator(string? tenantId)
    {
        SystemExecutionContext ctx = new(
            tenantId: tenantId,
            userId: "user1",
            username: "user1",
            correlationId: "corr",
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: [],
            sourceType: "test");

        ServiceCollection services = new();
        
        // Use the new AddMMediator to ensure all core infrastructure is present
        services.AddMMediator();

        // Register the specific behavior to its specific interface
        services.AddTransient<
            IPipelineBehavior<TenantRequest, string>, 
            MTenantValidationBehavior<TenantRequest, string>>();

        services.AddSingleton<ISystemExecutionContextAccessor>(
            new SystemExecutionContextAccessor(ctx));

        // Register handlers manually
        services.AddTransient<IRequestHandler<TenantRequest, string>, TenantHandler>();
        services.AddTransient<IRequestHandler<PlainRequest, string>, PlainHandler>();

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    // Simple accessor stub
    private sealed class SystemExecutionContextAccessor(ISystemExecutionContext ctx) : ISystemExecutionContextAccessor
    {
        public ISystemExecutionContext Get() => ctx;
        public void Set(ISystemExecutionContext context) { }
        public void Clear() { }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantRequest_WithValidTenant_Passes()
    {
        IMediator mediator = BuildMediator("tenant-abc");
        string result = await mediator.Send(new TenantRequest("hello"));
        Assert.Equal("handled:hello", result);
    }

    [Fact]
    public async Task TenantRequest_WithNullTenant_ThrowsUnauthorized()
    {
        IMediator mediator = BuildMediator(null);
        await Assert.ThrowsAsync<MUnauthorizedException>(
            () => mediator.Send(new TenantRequest("hello")));
    }

    [Fact]
    public async Task TenantRequest_WithEmptyTenant_ThrowsUnauthorized()
    {
        IMediator mediator = BuildMediator("   ");
        await Assert.ThrowsAsync<MUnauthorizedException>(
            () => mediator.Send(new TenantRequest("hello")));
    }

    [Fact]
    public async Task PlainRequest_NoTenantRequired_AlwaysPasses()
    {
        ServiceCollection services = new();
        services.AddMMediator(); // Standard core
        services.AddTransient<IRequestHandler<PlainRequest, string>, PlainHandler>();

        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        string result = await mediator.Send(new PlainRequest("world"));
        Assert.Equal("plain:world", result);
    }
}
