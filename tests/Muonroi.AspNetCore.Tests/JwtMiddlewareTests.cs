using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.AspNetCore.Tests;

public class JwtMiddlewareTests
{
    private static readonly IServiceProvider Provider = new ServiceCollection().BuildServiceProvider();

    private sealed class PassthroughTenantContextPolicy : ITenantContextPolicy
    {
        public bool IsTenantRequired => false;
        public bool IsUserRequired => false;

        public ISystemExecutionContext ResolveAndValidate(ISystemExecutionContext context)
        {
            return context;
        }
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        JwtMiddleware middleware = new(null!, null!, null!, null!);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_With_Valid_Dependencies()
    {
        JwtMiddleware middleware = new(
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(new MAuthenticateInfoContext(false)),
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());
        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task Invoke_ValidToken_Calls_Next()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        context.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext _)
        {
            called = true;
            return Task.CompletedTask;
        }

        MAuthenticateInfoContext info = new(true)
        {
            CurrentUserGuid = "u",
            CurrentUsername = "user",
            TokenValidityKey = "k",
            Permission = "1",
            TenantId = "tenant-1"
        };

        JwtMiddleware middleware = new(
            Next,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(info),
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());

        await middleware.Invoke(context, Provider);

        Assert.True(called);
        Assert.Equal("u", context.Request.Headers[nameof(MAuthenticateInfoContext.CurrentUserGuid)]);
        Assert.Equal("k", context.Request.Headers[nameof(MAuthenticateInfoContext.TokenValidityKey)]);
        Assert.Equal("user", context.User.FindFirst(nameof(MAuthenticateInfoContext.CurrentUsername))?.Value);
        Assert.Equal("tenant-1", context.User.FindFirst(ClaimConstants.TenantId)?.Value);
    }

    [Fact]
    public async Task Invoke_InvalidToken_Returns_Unauthorized()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        context.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext _)
        {
            called = true;
            return Task.CompletedTask;
        }

        JwtMiddleware middleware = new(
            Next,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(new MAuthenticateInfoContext(false)),
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());

        await middleware.Invoke(context, Provider);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_Adds_Default_Headers_When_Missing()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        context.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext _)
        {
            called = true;
            return Task.CompletedTask;
        }

        MAuthenticateInfoContext info = new(true)
        {
            CurrentUserGuid = "u",
            CurrentUsername = "user",
            TokenValidityKey = "k"
        };

        JwtMiddleware middleware = new(
            Next,
            (_, _) => Task.FromResult<IAuthenticateInfoContext>(info),
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());

        await middleware.Invoke(context, Provider);

        Assert.True(called);
        Assert.True(context.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.CorrelationId)));
        Assert.Equal("vi-VN", context.Request.Headers.AcceptLanguage);
    }

    [Fact]
    public async Task Invoke_AnonymousPath_Calls_Next_Without_Verification()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/swagger/index.html";
        bool called = false;
        bool callbackCalled = false;

        Task Next(HttpContext _)
        {
            called = true;
            return Task.CompletedTask;
        }

        JwtMiddleware middleware = new(
            Next,
            (_, _) =>
            {
                callbackCalled = true;
                return Task.FromResult<IAuthenticateInfoContext>(new MAuthenticateInfoContext(false));
            },
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());

        await middleware.Invoke(context, Provider);

        Assert.True(called);
        Assert.False(callbackCalled);
    }

    [Fact]
    public async Task Invoke_Propagates_Exception_From_Callback()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        context.SetEndpoint(endpoint);

        JwtMiddleware middleware = new(
            _ => Task.CompletedTask,
            (_, _) => throw new InvalidOperationException("fail"),
            new SystemExecutionContextAccessor(),
            new PassthroughTenantContextPolicy());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.Invoke(context, new ServiceCollection().BuildServiceProvider()));
    }

    private static bool InvokeIsAllowAnonymous(HttpContext context)
    {
        MethodInfo methodInfo = typeof(JwtMiddleware).GetMethod("IsAllowAnonymous", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)methodInfo.Invoke(null, [context])!;
    }

    private static void InvokeAddHeader(IHeaderDictionary headers, string key, string? value)
    {
        MethodInfo methodInfo = typeof(JwtMiddleware).GetMethod("AddHeader", BindingFlags.NonPublic | BindingFlags.Static)!;
        methodInfo.Invoke(null, [headers, key, value!]);
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsTrue_WhenEndpointAllows()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "test");
        context.SetEndpoint(endpoint);
        Assert.True(InvokeIsAllowAnonymous(context));
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsFalse_WhenEndpointNotAnonymous()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        context.SetEndpoint(endpoint);
        Assert.False(InvokeIsAllowAnonymous(context));
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsTrue_WhenNoEndpoint()
    {
        DefaultHttpContext context = new();
        Assert.True(InvokeIsAllowAnonymous(context));
    }

    [Fact]
    public void AddHeader_Adds_Header()
    {
        HeaderDictionary headers = [];
        InvokeAddHeader(headers, "k", "v");
        Assert.Equal("v", headers["k"].ToString());
    }

    [Fact]
    public void AddHeader_Replaces_Existing_Header()
    {
        HeaderDictionary headers = new() { { "k", "old" } };
        InvokeAddHeader(headers, "k", "new");
        Assert.Equal("new", headers["k"].ToString());
    }

    [Fact]
    public void AddHeader_Allows_Null_Or_Empty_Value()
    {
        HeaderDictionary headers = [];
        InvokeAddHeader(headers, "k", null);
        Assert.Equal(string.Empty, headers["k"].ToString());

        InvokeAddHeader(headers, "k", string.Empty);
        Assert.Equal(string.Empty, headers["k"].ToString());
    }
}
