namespace Muonroi.BuildingBlock.Test;

public class MExceptionMiddlewareTests
{
    private class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private class NullSerilogLogger : ILogger
    {
        public void Write(LogEvent logEvent)
        {
        }

        public void Write(LogEventLevel level, string messageTemplate)
        {
        }

        public void Write(LogEventLevel level, string messageTemplate, params object?[]? propertyValues)
        {
        }

        public void Write(LogEventLevel level, Exception? exception, string messageTemplate)
        {
        }

        public void Write(LogEventLevel level, Exception? exception, string messageTemplate,
            params object?[]? propertyValues)
        {
        }

        public bool IsEnabled(LogEventLevel level)
        {
            return false;
        }

        public ILogger ForContext(string propertyName, object? value, bool destructureObjects = false)
        {
            return this;
        }

        public ILogger ForContext<TSource>()
        {
            return this;
        }

        public ILogger ForContext(Type source)
        {
            return this;
        }
    }


    private static Task InvokeHandle(MExceptionMiddleware mw, HttpContext ctx, Exception? ex)
    {
        MethodInfo mi = typeof(MExceptionMiddleware)
            .GetMethod("HandleExceptionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)mi.Invoke(mw, [ctx, ex!])!;
    }

    [Fact]
    public void Constructor_Allows_Valid_Dependencies()
    {
        MExceptionMiddleware mw = new(_ => Task.CompletedTask, new NullSerilogLogger(), new MJsonSerializeService(),
            new MAuthenticateInfoContext(false), new FakeEnvironment());
        Assert.NotNull(mw);
    }

    [Fact]
    public void Constructor_Throws_For_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new MExceptionMiddleware(null!, new NullSerilogLogger(),
            new MJsonSerializeService(), new MAuthenticateInfoContext(false), new FakeEnvironment()));
    }

    [Fact]
    public async Task HandleExceptionAsync_Writes_Response()
    {
        DefaultHttpContext ctx = new();
        ctx.Response.Body = new MemoryStream();
        MExceptionMiddleware mw = new(_ => Task.CompletedTask, new NullSerilogLogger(), new MJsonSerializeService(),
            new MAuthenticateInfoContext(false) { Language = "en" }, new FakeEnvironment());
        await InvokeHandle(mw, ctx, new InvalidOperationException("fail"));
        ctx.Response.Body.Position = 0;
        string body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("UnhandledException", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task HandleExceptionAsync_Null_Exception_Throws()
    {
        DefaultHttpContext ctx = new();
        MExceptionMiddleware mw = new(_ => Task.CompletedTask, new NullSerilogLogger(), new MJsonSerializeService(),
            new MAuthenticateInfoContext(false), new FakeEnvironment());
        await Assert.ThrowsAsync<NullReferenceException>(() => InvokeHandle(mw, ctx, null));
    }

    [Fact]
    public async Task InvokeAsync_Catches_Exception_And_Returns_Error()
    {
        DefaultHttpContext ctx = new();
        ctx.Response.Body = new MemoryStream();

        static Task Next(HttpContext _)
        {
            throw new Exception("boom");
        }

        MExceptionMiddleware mw = new(Next, new NullSerilogLogger(), new MJsonSerializeService(),
            new MAuthenticateInfoContext(false) { Language = "en" }, new FakeEnvironment());
        await mw.InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        string body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("UnhandledException", body);
        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
    }
}
