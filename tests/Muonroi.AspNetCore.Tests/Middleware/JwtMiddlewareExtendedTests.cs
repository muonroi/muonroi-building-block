namespace Muonroi.AspNetCore.Tests.Middleware;

public class JwtMiddlewareExtendedTests
{
    private readonly ISystemExecutionContextAccessor _contextAccessor = Substitute.For<ISystemExecutionContextAccessor>();
    private readonly ITenantContextPolicy _tenantContextPolicy = Substitute.For<ITenantContextPolicy>();

    private JwtMiddleware CreateMiddleware(
        RequestDelegate next,
        Func<IServiceProvider, HttpContext, Task<IAuthenticateInfoContext>>? callback = null)
    {
        callback ??= (_, _) => Task.FromResult<IAuthenticateInfoContext>(new MAuthenticateInfoContext(false));
        return new JwtMiddleware(next, callback, _contextAccessor, _tenantContextPolicy);
    }

    [Fact]
    public async Task Invoke_SwaggerPath_SkipsAuthentication()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/swagger/index.html";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_HealthPath_SkipsAuthentication()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/health";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_FaviconPath_SkipsAuthentication()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/favicon.ico";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_NoEndpoint_AllowsAnonymous()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/data";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        // No endpoint set - null endpoint = allow anonymous

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_AutoAddsBearerPrefix_WhenMissing()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/test";
        httpContext.Request.Headers.Authorization = "some-raw-token";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        // No endpoint = allow anonymous path, but header modification happens first

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        // After middleware, the header should have Bearer prefix
        httpContext.Request.Headers.Authorization.ToString().Should().StartWith("Bearer ");
    }

    [Fact]
    public async Task Invoke_DoesNotDuplicateBearerPrefix()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/test";
        httpContext.Request.Headers.Authorization = "Bearer existing-token";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        httpContext.Request.Headers.Authorization.ToString().Should().Be("Bearer existing-token");
    }

    [Fact]
    public async Task Invoke_RemovesSensitiveHeaders()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/test";
        httpContext.Request.Headers[nameof(MAuthenticateInfoContext.CurrentUserGuid)] = "spoofed-guid";
        httpContext.Request.Headers[nameof(MAuthenticateInfoContext.CurrentUsername)] = "spoofed-user";
        httpContext.Request.Headers[nameof(MAuthenticateInfoContext.Permission)] = "spoofed-perm";
        httpContext.Request.Headers[nameof(MAuthenticateInfoContext.TokenValidityKey)] = "spoofed-key";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        // No endpoint = allow anonymous, but sensitive headers still removed

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        // Sensitive headers should be removed
        httpContext.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.CurrentUserGuid)).Should().BeFalse();
        httpContext.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.CurrentUsername)).Should().BeFalse();
        httpContext.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.Permission)).Should().BeFalse();
        httpContext.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.TokenValidityKey)).Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_UnauthenticatedToken_Returns401()
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(new MAuthenticateInfoContext(false)));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/protected";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        // Set up endpoint with Authorize attribute
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "TestEndpoint");
        httpContext.SetEndpoint(endpoint);

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invoke_AuthenticatedToken_SetsUserClaims()
    {
        var authInfo = new MAuthenticateInfoContext(true)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
            CurrentUsername = "testuser",
            TenantId = "tenant-1",
            Permission = "read,write"
        };

        _tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(args =>
            {
                var ctx = (SystemExecutionContext)args[0];
                return ctx;
            });

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(authInfo));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/protected";
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "TestEndpoint");
        httpContext.SetEndpoint(endpoint);

        await middleware.Invoke(httpContext, httpContext.RequestServices);

        httpContext.User.Identity.Should().NotBeNull();
        httpContext.User.Identity!.IsAuthenticated.Should().BeTrue();
    }
}
