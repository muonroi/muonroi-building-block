namespace Muonroi.Tenancy.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Invoke_Uses_Header_Tenant_And_Clears_After_Request()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-header");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-header")
        ], "test"));

        bool nextCalled = false;
        TenantResolutionMiddleware middleware = new(async _ =>
        {
            nextCalled = true;
            Muonroi.Tenancy.Core.TenantContext.CurrentTenantId.Should().Be("tenant-header");
            await Task.CompletedTask;
        });

        await middleware.Invoke(context);

        nextCalled.Should().BeTrue();
        Muonroi.Tenancy.Core.TenantContext.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task Invoke_Returns_401_When_Claim_Mismatches_Resolved_Tenant()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-header");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-claim")
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Invoke_Uses_Path_Segment_When_Header_Is_Missing()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/tenant-a/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-a")
        ], "test"));

        string? captured = null;
        TenantResolutionMiddleware middleware = new(_ =>
        {
            captured = Muonroi.Tenancy.Core.TenantContext.CurrentTenantId;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        captured.Should().Be("tenant-a");
    }
}
