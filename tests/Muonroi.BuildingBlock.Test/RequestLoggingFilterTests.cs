namespace Muonroi.BuildingBlock.Test;

public class RequestLoggingFilterTests
{
    private class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "app";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private class ListLogger<T> : ILogger<T>, IDisposable
    {
        public readonly List<object?> States = [];
        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // No managed resources to dispose in this class.
            }


            _disposed = true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return this;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            States.Add(state);
        }
    }

    private class ThrowLogger<T> : ILogger<T>
    {
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("log fail");
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullDisposable.Instance;
        }

        private class NullDisposable : IDisposable
        {
            private bool _disposed;

            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }
        }
    }

    private class DummySanitizer : ILogSanitizer
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
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SiteCode"] = "s" }).Build();
        RequestLoggingFilter filter = new(logger, new MJsonSerializeService(), new MAuthenticateInfoContext(false),
            config, new FakeEnvironment(), new DummySanitizer());
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?> { ["secret"] = "value" },
            new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
        Assert.Equal(2, logger.States.Count);
        string log = logger.States[0]?.ToString() ?? string.Empty;
        Assert.DoesNotContain("value", log);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Allows_NullLogger()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        RequestLoggingFilter filter = new(NullLogger<RequestLoggingFilter>.Instance, new MJsonSerializeService(),
            new MAuthenticateInfoContext(false), config, new FakeEnvironment());
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        await filter.OnActionExecutionAsync(exc,
            () => Task.FromResult(new ActionExecutedContext(ac, [], new object())));
        Assert.NotNull(exc);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Throws_When_Logger_Fails()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        RequestLoggingFilter filter = new(new ThrowLogger<RequestLoggingFilter>(), new MJsonSerializeService(),
            new MAuthenticateInfoContext(false), config, new FakeEnvironment());
        DefaultHttpContext ctx = new();
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.OnActionExecutionAsync(exc, () => Task.FromResult(new ActionExecutedContext(ac, [], new object()))));
    }

    [Fact]
    public void Constructor_Creates_Instance()
    {
        ILogger<RequestLoggingFilter> log = Substitute.For<ILogger<RequestLoggingFilter>>();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        RequestLoggingFilter filter = new(log, json, new MAuthenticateInfoContext(false), cfg, env);
        Assert.NotNull(filter);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        RequestLoggingFilter filter = new(null!, null!, null!, null!, null!);
        Assert.NotNull(filter);
    }
}
