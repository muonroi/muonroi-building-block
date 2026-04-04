using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.AspNetCore.Tests.Middleware;

public class LicenseMiddlewareTests
{
    [Fact]
    public void Constructor_NullNext_ThrowsArgumentNullException()
    {
        Action act = () => new LicenseMiddleware(null!);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public async Task InvokeAsync_EnforceDisabled_CallsNextWithoutGuard()
    {
        bool nextCalled = false;
        var middleware = new LicenseMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var guard = Substitute.For<ILicenseGuard>();
        var configs = new LicenseConfigs { EnforceOnMiddleware = false };
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext, guard, configs);

        nextCalled.Should().BeTrue();
        guard.DidNotReceive().EnsureValid(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task InvokeAsync_EnforceEnabled_CallsGuardEnsureValid()
    {
        bool nextCalled = false;
        var middleware = new LicenseMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var guard = Substitute.For<ILicenseGuard>();
        var configs = new LicenseConfigs { EnforceOnMiddleware = true };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/test";

        await middleware.InvokeAsync(httpContext, guard, configs);

        nextCalled.Should().BeTrue();
        guard.Received(1).EnsureValid("api.request", "/api/v1/test");
    }

    [Fact]
    public async Task InvokeAsync_EnforceEnabled_RecordsAction()
    {
        var middleware = new LicenseMiddleware(_ => Task.CompletedTask);
        var guard = Substitute.For<ILicenseGuard>();
        var configs = new LicenseConfigs { EnforceOnMiddleware = true };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/v1/data";

        await middleware.InvokeAsync(httpContext, guard, configs);

        guard.Received(1).RecordAction(Arg.Is<LicenseActionContext>(ctx =>
            ctx.ActionType == "api.request" &&
            ctx.ActionName == "/api/v1/data"));
    }

    [Fact]
    public async Task InvokeAsync_EnforceEnabled_TenantFromHeader()
    {
        var middleware = new LicenseMiddleware(_ => Task.CompletedTask);
        var guard = Substitute.For<ILicenseGuard>();
        var configs = new LicenseConfigs { EnforceOnMiddleware = true };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CustomHeader.TenantId] = "test-tenant";

        await middleware.InvokeAsync(httpContext, guard, configs);

        guard.Received(1).RecordAction(Arg.Is<LicenseActionContext>(ctx =>
            ctx.TenantId == "test-tenant"));
    }

    [Fact]
    public async Task InvokeAsync_EnforceEnabled_TenantFromClaim()
    {
        var middleware = new LicenseMiddleware(_ => Task.CompletedTask);
        var guard = Substitute.For<ILicenseGuard>();
        var configs = new LicenseConfigs { EnforceOnMiddleware = true };
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimConstants.TenantId, "claim-tenant") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        await middleware.InvokeAsync(httpContext, guard, configs);

        guard.Received(1).RecordAction(Arg.Is<LicenseActionContext>(ctx =>
            ctx.TenantId == "claim-tenant"));
    }
}
