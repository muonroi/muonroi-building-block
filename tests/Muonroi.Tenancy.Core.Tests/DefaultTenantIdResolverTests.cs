using Muonroi.Tenancy.Core;

namespace Muonroi.Tenancy.Core.Tests;

public class DefaultTenantIdResolverTests
{
    [Fact]
    public async Task ResolveTenantIdAsync_ReturnsClaimTenantId()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimConstants.TenantId, "claimTenant")
            ]))
        };

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("claimTenant", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ReturnsHeaderTenantId_WhenNoClaim()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "headerTenant");

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("headerTenant", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ParsesSubdomain_WhenNoClaimOrHeader()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("tenant.example.com");

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("tenant", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ParsesPath_WhenNoClaimOrHeader()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/tenant-x/orders";

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("tenant-x", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_DoesNotTreatApiPrefixAsTenant()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/api/v1/orders";

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Null(tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_UsesRouteValueTenantId_WhenAvailable()
    {
        DefaultHttpContext context = new();
        context.Request.RouteValues["tenantId"] = "route-tenant";
        context.Request.Path = "/api/tenant/route-tenant/orders";

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("route-tenant", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_ParsesApiTenantRoute_WhenRouteValuesMissing()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/api/tenant/path-tenant/orders";

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Equal("path-tenant", tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_DoesNotTreatIpv4AsSubdomainTenant()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("127.0.0.1");

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Null(tenantId);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_DoesNotTreatIpv6AsSubdomainTenant()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("::1");

        DefaultTenantIdResolver resolver = new();
        string? tenantId = await resolver.ResolveTenantIdAsync(context);

        Assert.Null(tenantId);
    }
}
