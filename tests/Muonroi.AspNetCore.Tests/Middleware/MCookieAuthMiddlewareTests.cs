namespace Muonroi.AspNetCore.Tests.Middleware;

public class MCookieAuthMiddlewareTests
{
    private readonly IMLog<MCookieAuthMiddleware> _logger = Substitute.For<IMLog<MCookieAuthMiddleware>>();

    [Fact]
    public async Task InvokeAsync_NoCookie_CallsNextWithoutModifyingHeaders()
    {
        bool nextCalled = false;
        var middleware = new MCookieAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _logger);
        var httpContext = new DefaultHttpContext();
        var tokenInfo = new MTokenInfo { CookieName = "auth_token", SymmetricSecretKey = "test-key-1234567890123456" };

        await middleware.InvokeAsync(httpContext, tokenInfo);

        nextCalled.Should().BeTrue();
        httpContext.Request.Headers.Authorization.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_InvalidCookie_LogsErrorAndCallsNext()
    {
        bool nextCalled = false;
        var middleware = new MCookieAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _logger);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("Cookie", "auth_token=invalid_encrypted_data");
        var tokenInfo = new MTokenInfo { CookieName = "auth_token", SymmetricSecretKey = "test-key-1234567890123456" };

        await middleware.InvokeAsync(httpContext, tokenInfo);

        nextCalled.Should().BeTrue();
        _logger.Received().Error(Arg.Any<Exception>(), Arg.Any<string>());
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        bool nextCalled = false;
        var middleware = new MCookieAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _logger);
        var httpContext = new DefaultHttpContext();
        var tokenInfo = new MTokenInfo { CookieName = "any_cookie", SymmetricSecretKey = "key" };

        await middleware.InvokeAsync(httpContext, tokenInfo);

        nextCalled.Should().BeTrue();
    }
}
