using Muonroi.Core.Abstractions.Exceptions;
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

    private static Task InvokeHandle(MExceptionMiddleware middleware, HttpContext context, Exception? exception)
    {
        MethodInfo methodInfo = typeof(MExceptionMiddleware)
            .GetMethod("HandleExceptionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)methodInfo.Invoke(middleware, [context, exception!])!;
    }

    [Fact]
    public void Constructor_Allows_Valid_Dependencies()
    {
        MExceptionMiddleware middleware = new(
            _ => Task.CompletedTask,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            new FakeEnvironment());
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_Throws_For_Null_Next()
    {
        Assert.Throws<MArgumentException>(() => new MExceptionMiddleware(
            null!,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            new FakeEnvironment()));
    }

    [Fact]
    public async Task HandleExceptionAsync_Writes_Response()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(
            _ => Task.CompletedTask,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false) { Language = "en" },
            new FakeEnvironment());

        await InvokeHandle(middleware, context, new InvalidOperationException("fail"));

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("UNHANDLED_EXCEPTION", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleExceptionAsync_Null_Exception_Throws()
    {
        DefaultHttpContext context = new();
        MExceptionMiddleware middleware = new(
            _ => Task.CompletedTask,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            new FakeEnvironment());

        var ex = await Assert.ThrowsAsync<TargetInvocationException>(() => InvokeHandle(middleware, context, null));
        Assert.IsType<MArgumentException>(ex.InnerException);
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

        MExceptionMiddleware middleware = new(
            Next,
            Substitute.For<IMLog<MExceptionMiddleware>>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false) { Language = "en" },
            new FakeEnvironment());

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("UNHANDLED_EXCEPTION", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}

