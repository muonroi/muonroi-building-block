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

    [Fact]
    public async Task Invoke_Returns_400_When_Header_Contains_Injection_Characters()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant'; DROP TABLE MRoles;--");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "valid-tenant")
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Invoke_Returns_400_When_Path_Contains_Injection_Characters()
    {
        DefaultHttpContext context = new();
        // Path segment with traversal characters
        context.Request.Path = "/../../etc/passwd";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "valid-tenant")
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Invoke_Accepts_Valid_Tenant_Id_With_Dots_Hyphens()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "org-123.prod");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "org-123.prod")
        ], "test"));

        bool nextCalled = false;
        TenantResolutionMiddleware middleware = new(async _ =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        await middleware.Invoke(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_Returns_400_When_Tenant_Id_Exceeds_64_Chars()
    {
        // 65 alphanumeric characters — exceeds max of 64
        string longId = new('a', 65);
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, longId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, longId)
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
