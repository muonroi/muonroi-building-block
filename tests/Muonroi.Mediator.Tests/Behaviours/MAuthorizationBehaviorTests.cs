using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Exceptions;
using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Attributes;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Tests.Behaviours;

public class MAuthorizationBehaviorTests
{
    // ── Request types ──────────────────────────────────────────────────────────

    [MAuthorize(Roles = "admin,manager")]
    private record AdminRequest : IRequest<string>;

    [MAuthorize(Permissions = "orders.read,orders.write")]
    private record OrderRequest : IRequest<string>;

    [MAuthorize(Roles = "superuser")]
    [MAuthorize(Permissions = "system.manage")]
    private record SuperRequest : IRequest<string>;

    private record NoAuthRequest : IRequest<string>;

    private class StringHandler<TReq> : IRequestHandler<TReq, string>
        where TReq : IRequest<string>
    {
        public Task<string> Handle(TReq request, CancellationToken ct)
            => Task.FromResult("ok");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IMediator BuildMediator<TRequest>(IReadOnlyList<string> permissions)
        where TRequest : IRequest<string>
    {
        SystemExecutionContext ctx = new(
            tenantId: "t1",
            userId: "u1",
            username: "u1",
            correlationId: "c1",
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: permissions,
            sourceType: "test");

        ServiceCollection services = new();
        services.AddSingleton<ISystemExecutionContextAccessor>(
            new ContextAccessorStub(ctx));
        services.AddTransient<IRequestHandler<TRequest, string>, StringHandler<TRequest>>();
        services.AddTransient<IPipelineBehavior<TRequest, string>, MAuthorizationBehavior<TRequest, string>>();
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    private sealed class ContextAccessorStub(ISystemExecutionContext ctx) : ISystemExecutionContextAccessor
    {
        public ISystemExecutionContext Get() => ctx;
        public void Set(ISystemExecutionContext context) { }
        public void Clear() { }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoleAuthorization_Passes_WhenRolePresent()
    {
        IMediator mediator = BuildMediator<AdminRequest>(["manager", "viewer"]);
        string result = await mediator.Send(new AdminRequest());
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task RoleAuthorization_Throws_WhenRoleAbsent()
    {
        IMediator mediator = BuildMediator<AdminRequest>(["viewer"]);
        await Assert.ThrowsAsync<MForbiddenException>(() => mediator.Send(new AdminRequest()));
    }

    [Fact]
    public async Task PermissionAuthorization_Passes_WhenAllPermissionsPresent()
    {
        IMediator mediator = BuildMediator<OrderRequest>(["orders.read", "orders.write", "orders.delete"]);
        string result = await mediator.Send(new OrderRequest());
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task PermissionAuthorization_Throws_WhenAnyPermissionAbsent()
    {
        IMediator mediator = BuildMediator<OrderRequest>(["orders.read"]); // missing orders.write
        await Assert.ThrowsAsync<MForbiddenException>(() => mediator.Send(new OrderRequest()));
    }

    [Fact]
    public async Task MultipleAttributes_AllMustPass()
    {
        // SuperRequest needs both "superuser" role AND "system.manage" permission
        IMediator mediator = BuildMediator<SuperRequest>(["superuser", "system.manage"]);
        string result = await mediator.Send(new SuperRequest());
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task MultipleAttributes_PartialFail_Throws()
    {
        IMediator mediator = BuildMediator<SuperRequest>(["superuser"]); // missing system.manage
        await Assert.ThrowsAsync<MForbiddenException>(() => mediator.Send(new SuperRequest()));
    }

    [Fact]
    public async Task NoAttribute_AlwaysPasses()
    {
        IMediator mediator = BuildMediator<NoAuthRequest>([]);
        string result = await mediator.Send(new NoAuthRequest());
        Assert.Equal("ok", result);
    }
}
