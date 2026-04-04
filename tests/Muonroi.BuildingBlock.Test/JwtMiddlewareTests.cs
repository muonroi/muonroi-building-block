using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class JwtMiddlewareTests
{
    private static readonly IServiceProvider Provider = new ServiceCollection().BuildServiceProvider();


    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        JwtMiddleware mw = new(null!, null!);
        Assert.NotNull(mw);
    }

    [Fact]
    public void Constructor_With_Valid_Dependencies()
    {
        JwtMiddleware mw = new(_ => Task.CompletedTask, (_, _) => Task.FromResult(new MAuthenticateInfoContext(false)));
        Assert.NotNull(mw);
    }

    [Fact]
    public async Task Invoke_ValidToken_Calls_Next()
    {
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "test");
        ctx.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext c)
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
        JwtMiddleware mw = new(Next, (_, _) => Task.FromResult(info));
        await mw.Invoke(ctx, Provider);
        Assert.True(called);
        Assert.Equal("u", ctx.Request.Headers[nameof(MAuthenticateInfoContext.CurrentUserGuid)]);
        Assert.Equal("k", ctx.Request.Headers[nameof(MAuthenticateInfoContext.TokenValidityKey)]);
        Assert.Equal("user", ctx.User.FindFirst(nameof(MAuthenticateInfoContext.CurrentUsername))?.Value);
        Assert.Equal("tenant-1", ctx.User.FindFirst(ClaimConstants.TenantId)?.Value);
    }

    [Fact]
    public async Task Invoke_InvalidToken_Returns_Unauthorized()
    {
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "test");
        ctx.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext c)
        {
            called = true;
            return Task.CompletedTask;
        }

        JwtMiddleware mw = new(Next, (_, _) => Task.FromResult(new MAuthenticateInfoContext(false)));
        await mw.Invoke(ctx, Provider);
        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_Adds_Default_Headers_When_Missing()
    {
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "test");
        ctx.SetEndpoint(endpoint);
        bool called = false;

        Task Next(HttpContext c)
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
        JwtMiddleware mw = new(Next, (_, _) => Task.FromResult(info));
        await mw.Invoke(ctx, Provider);
        Assert.True(called);
        Assert.True(ctx.Request.Headers.ContainsKey(nameof(MAuthenticateInfoContext.CorrelationId)));
        Assert.Equal("vi-VN", ctx.Request.Headers.AcceptLanguage);
    }

    [Fact]
    public async Task Invoke_Propagates_Exception_From_Callback()
    {
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "test");
        ctx.SetEndpoint(endpoint);
        JwtMiddleware mw = new(_ => Task.CompletedTask, (_, _) => throw new InvalidOperationException("fail"));
        await Assert.ThrowsAsync<MInternalException>(() => mw.Invoke(ctx, Provider));
    }

    private static bool InvokeIsAllowAnonymous(HttpContext context)
    {
        MethodInfo mi = typeof(JwtMiddleware).GetMethod("IsAllowAnonymous", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)mi.Invoke(null, [context])!;
    }

    private static void InvokeAddHeader(IHeaderDictionary headers, string key, string value)
    {
        MethodInfo mi = typeof(JwtMiddleware).GetMethod("AddHeader", BindingFlags.NonPublic | BindingFlags.Static)!;
        mi.Invoke(null, [headers, key, value]);
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsTrue_WhenEndpointAllows()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "test");
        context.SetEndpoint(endpoint);
        Assert.True(InvokeIsAllowAnonymous(context));
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsFalse_WhenEndpointNotAnonymous()
    {
        DefaultHttpContext context = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "test");
        context.SetEndpoint(endpoint);
        Assert.False(InvokeIsAllowAnonymous(context));
    }

    [Fact]
    public void IsAllowAnonymous_ReturnsFalse_WhenNoEndpoint()
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
        InvokeAddHeader(headers, "k", null!);
        Assert.Equal(string.Empty, headers["k"].ToString());
        InvokeAddHeader(headers, "k", string.Empty);
        Assert.Equal(string.Empty, headers["k"].ToString());
    }
}
