using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Muonroi.Core.Abstractions.Ecosystem;

namespace Muonroi.Core.Abstractions.Tests.Ecosystem;

public class MEcosystemStartupFilterTests
{
    private static MEcosystemStartupFilter CreateFilter(IMEcosystemRegistry registry)
    {
        ILogger<MEcosystemStartupFilter> logger = NullLogger<MEcosystemStartupFilter>.Instance;
        return new MEcosystemStartupFilter(registry, logger);
    }

    [Fact]
    public void Configure_WhenActiveIsNone_ReturnsNextUnchanged()
    {
        var registry = new MEcosystemRegistry();
        var filter = CreateFilter(registry);

        bool nextCalled = false;
        Action<IApplicationBuilder> next = _ => nextCalled = true;

        Action<IApplicationBuilder> result = filter.Configure(next);

        // Configure returns next unchanged (logging-only filter)
        result.Should().BeSameAs(next);
    }

    [Fact]
    public void Configure_WhenActiveIsNone_LogsNone()
    {
        var registry = new MEcosystemRegistry();
        var capturingLogger = new CapturingLogger<MEcosystemStartupFilter>();
        var filter = new MEcosystemStartupFilter(registry, capturingLogger);
        Action<IApplicationBuilder> next = _ => { };

        filter.Configure(next);

        capturingLogger.LastMessage.Should().Contain("None");
    }

    [Fact]
    public void Configure_WhenLoggingRegistered_LogsLogging()
    {
        var registry = new MEcosystemRegistry();
        registry.Register(MCapability.Logging);
        var capturingLogger = new CapturingLogger<MEcosystemStartupFilter>();
        var filter = new MEcosystemStartupFilter(registry, capturingLogger);
        Action<IApplicationBuilder> next = _ => { };

        filter.Configure(next);

        capturingLogger.LastMessage.Should().Contain("Logging");
    }

    [Fact]
    public void Configure_WhenMultipleCapabilitiesRegistered_LogsAllActiveFlags()
    {
        var registry = new MEcosystemRegistry();
        registry.Register(MCapability.Logging);
        registry.Register(MCapability.RuleEngine);
        var capturingLogger = new CapturingLogger<MEcosystemStartupFilter>();
        var filter = new MEcosystemStartupFilter(registry, capturingLogger);
        Action<IApplicationBuilder> next = _ => { };

        filter.Configure(next);

        capturingLogger.LastMessage.Should().Contain("Logging");
        capturingLogger.LastMessage.Should().Contain("RuleEngine");
    }

    [Fact]
    public void Configure_MessageContainsEcosystemPrefix()
    {
        var registry = new MEcosystemRegistry();
        var capturingLogger = new CapturingLogger<MEcosystemStartupFilter>();
        var filter = new MEcosystemStartupFilter(registry, capturingLogger);
        Action<IApplicationBuilder> next = _ => { };

        filter.Configure(next);

        capturingLogger.LastMessage.Should().Contain("[Muonroi.Ecosystem] Active:");
    }

    [Fact]
    public void AddEcosystemStartupLog_RegistersStartupFilter_InServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddEcosystemStartupLog();
        services.Should().Contain(sd => sd.ServiceType == typeof(IStartupFilter));
    }

    [Fact]
    public void GetOrCreateRegistry_AutoRegistersStartupFilter()
    {
        var services = new ServiceCollection();
        services.GetOrCreateRegistry();
        services.Should().Contain(sd => sd.ServiceType == typeof(IStartupFilter));
    }

    /// <summary>
    /// Minimal capturing logger for test assertions.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public string? LastMessage { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LastMessage = formatter(state, exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
