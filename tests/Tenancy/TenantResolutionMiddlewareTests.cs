using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Tenancy.Core;
using Muonroi.Tenancy;

namespace Muonroi.Tenancy.Tests;

public class TenantResolutionMiddlewareTests
{
    private static ClaimsPrincipal CreatePrincipal(string tenantId)
    {
        ClaimsIdentity identity = new([new Claim(ClaimConstants.TenantId, tenantId)]);
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Resolve_From_Header_Sets_TenantContext_And_Activity_Tag()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "headerTenant");
        context.User = CreatePrincipal("headerTenant");

        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            Assert.Equal("headerTenant", TenantContext.CurrentTenantId);
            return Task.CompletedTask;
        };

        using Activity activity = new("test");
        activity.Start();

        TenantResolutionMiddleware middleware = new(next);
        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Null(TenantContext.CurrentTenantId);
        Assert.Equal("headerTenant", activity.GetTagItem("tenant.id"));
        activity.Stop();
    }

    [Fact]
    public async Task Resolve_From_Path()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/pathTenant/api";
        context.User = CreatePrincipal("pathTenant");

        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            Assert.Equal("pathTenant", TenantContext.CurrentTenantId);
            return Task.CompletedTask;
        };

        TenantResolutionMiddleware middleware = new(next);
        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task Resolve_From_Subdomain()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("tenant.example.com");
        context.User = CreatePrincipal("tenant");

        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            Assert.Equal("tenant", TenantContext.CurrentTenantId);
            return Task.CompletedTask;
        };

        TenantResolutionMiddleware middleware = new(next);
        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task Mismatched_Claim_Returns_Unauthorized()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "headerTenant");
        context.User = CreatePrincipal("otherTenant");

        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        TenantResolutionMiddleware middleware = new(next);
        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Null(TenantContext.CurrentTenantId);
    }
}
