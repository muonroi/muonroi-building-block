using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Tenancy.Abstractions;
namespace Muonroi.AspNetCore.Tests;

public class MExceptionMiddlewareTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static Task InvokeHandle(
        MExceptionMiddleware middleware, 
        HttpContext context, 
        Exception? exception,
        IMLog<MExceptionMiddleware>? logger = null,
        IMJsonSerializeService? serializeService = null,
        MAuthenticateInfoContext? authContext = null,
        IHostEnvironment? environment = null,
        ITenantContext? tenantContext = null)
    {
        MethodInfo methodInfo = typeof(MExceptionMiddleware)
            .GetMethod("HandleExceptionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        
        return (Task)methodInfo.Invoke(middleware, [
            context, 
            exception!, 
            logger ?? Substitute.For<IMLog<MExceptionMiddleware>>(),
            serializeService ?? new MJsonSerializeService(),
            authContext ?? new MAuthenticateInfoContext(false),
            environment ?? new FakeEnvironment(),
            tenantContext
        ])!;
    }

    [Fact]
    public void Constructor_Allows_Valid_Dependencies()
    {
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_Throws_For_Null_Next()
    {
        Assert.Throws<MArgumentException>(() => new MExceptionMiddleware(null!));
    }

    [Fact]
    public async Task HandleExceptionAsync_Writes_Response()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        await InvokeHandle(middleware, context, new MInternalException("fail"),
            authContext: new MAuthenticateInfoContext(false) { Language = "en" });

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleExceptionAsync_Enriches_Log_Scope()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        var logger = Substitute.For<IMLog<MExceptionMiddleware>>();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);
        
        var authContext = new MAuthenticateInfoContext(true) 
        { 
            TenantId = "test-tenant",
            CurrentUserGuid = "test-user"
        };

        await InvokeHandle(middleware, context, new MInternalException("fail"), 
            logger: logger, 
            authContext: authContext);

        // Verify BeginScope was called with expected fields
        logger.Received().BeginScope(Arg.Is<IDictionary<string, object?>>(scope => 
            scope.ContainsKey("Layer") && 
            scope["TenantId"] != null && scope["TenantId"]!.ToString() == "test-tenant" &&
            scope["UserId"] != null && scope["UserId"]!.ToString() == "test-user" &&
            scope["ErrorCode"] != null && scope["ErrorCode"]!.ToString() == "INTERNAL_ERROR"
        ));
    }

    [Fact]
    public async Task InvokeAsync_Catches_Exception_And_Returns_Error()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        static Task Next(HttpContext _)
        {
            throw new Exception("boom");
        }

        MExceptionMiddleware middleware = new(Next);

        await middleware.InvokeAsync(
            context,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false) { Language = "en" },
            new FakeEnvironment());

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("UNHANDLED_EXCEPTION", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}

