namespace Muonroi.AspNetCore.Tests;

public class RequestLoggingFilterTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "app";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger<T> : ILogger<T>, IDisposable
    {
        public readonly List<object?> States = [];
        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return this;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            States.Add(state);
        }
    }

    private sealed class ThrowLogger<T> : ILogger<T>
    {
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("log fail");
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullDisposable.Instance;
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose()
            {
            }
        }
    }

    private sealed class DummySanitizer : ILogSanitizer
    {
        public IDictionary<string, object?> Sanitize(IDictionary<string, object?> data)
        {
            data["secret"] = "***";
            return data;
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_Logs_Request()
    {
        ListLogger<RequestLoggingFilter> logger = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SiteCode"] = "s" })
            .Build();

        RequestLoggingFilter filter = new(
            logger,
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            config,
            new FakeEnvironment(),
            new DummySanitizer());

        DefaultHttpContext context = new();
        ActionContext actionContext = new(context, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(
            actionContext,
            [],
            new Dictionary<string, object?> { ["secret"] = "value" },
            new object());

        bool called = false;
        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        }

        await filter.OnActionExecutionAsync(executingContext, Next);

        Assert.True(called);
        Assert.Equal(2, logger.States.Count);
        string allLogs = string.Join(" ", logger.States.Select(x => x?.ToString() ?? string.Empty));
        Assert.DoesNotContain("value", allLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Allows_NullLogger()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        RequestLoggingFilter filter = new(
            NullLogger<RequestLoggingFilter>.Instance,
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            config,
            new FakeEnvironment());

        DefaultHttpContext context = new();
        ActionContext actionContext = new(context, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), new object());

        await filter.OnActionExecutionAsync(
            executingContext,
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object())));

        Assert.NotNull(executingContext);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Throws_When_Logger_Fails()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        RequestLoggingFilter filter = new(
            new ThrowLogger<RequestLoggingFilter>(),
            new MJsonSerializeService(),
            new MAuthenticateInfoContext(false),
            config,
            new FakeEnvironment());

        DefaultHttpContext context = new();
        ActionContext actionContext = new(context, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), new object());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.OnActionExecutionAsync(
                executingContext,
                () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object()))));
    }

    [Fact]
    public void Constructor_Creates_Instance()
    {
        ILogger<RequestLoggingFilter> logger = Substitute.For<ILogger<RequestLoggingFilter>>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();

        RequestLoggingFilter filter = new(
            logger,
            json,
            new MAuthenticateInfoContext(false),
            config,
            environment);

        Assert.NotNull(filter);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        RequestLoggingFilter filter = new(null!, null!, null!, null!, null!);
        Assert.NotNull(filter);
    }
}
