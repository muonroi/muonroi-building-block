namespace Muonroi.Logging.Tests;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Logging.Abstractions;
using Moq;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;

public class MLogMethodTests
{
    private readonly Mock<ILogger<MLogMethodTests>> _loggerMock = new();
    private readonly Mock<ISystemExecutionContextAccessor> _accessorMock = new();
    private readonly Mock<IMLogContext> _logContextMock = new();
    private readonly MLog<MLogMethodTests> _sut;

    public MLogMethodTests()
    {
        _accessorMock.Setup(x => x.Get()).Returns(SystemExecutionContext.Empty);
        _logContextMock.Setup(x => x.PushProperty(It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Mock.Of<IMLogContextScope>());
        _sut = new MLog<MLogMethodTests>(_loggerMock.Object, _accessorMock.Object, _logContextMock.Object);
    }

    [Fact]
    public void Warn_Should_Log_Warning()
    {
        var ex = Record.Exception(() => _sut.Warn("Warning: {msg}", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void Error_Should_Log_Error_With_Exception()
    {
        var ex = Record.Exception(() => _sut.Error(new InvalidOperationException("boom"), "Error: {msg}", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void Debug_Should_Log_Debug()
    {
        var ex = Record.Exception(() => _sut.Debug("Debug: {msg}", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void InfoTrace_Should_Log_Trace()
    {
        var ex = Record.Exception(() => _sut.InfoTrace("Trace: {msg}", "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void IsEnabled_Should_Delegate_To_Inner()
    {
        _loggerMock.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
        Assert.True(_sut.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void IsEnabled_Should_Return_False_When_Inner_Returns_False()
    {
        _loggerMock.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(false);
        Assert.False(_sut.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void BeginProperty_Should_Delegate_To_LogContext()
    {
        var scopeMock = new Mock<IMLogContextScope>();
        _logContextMock.Setup(x => x.PushProperty("key", "value")).Returns(scopeMock.Object);

        IMLogContextScope result = _sut.BeginProperty("key", "value");

        Assert.Same(scopeMock.Object, result);
    }

    [Fact]
    public void BeginScope_Should_Create_Combined_Scope()
    {
        _accessorMock.Setup(x => x.Get()).Returns(new SystemExecutionContext(
            "t1", "u1", "admin", "corr", null, null, true, null, "test"));

        using IDisposable? scope = _sut.BeginScope("test-state");
        Assert.NotNull(scope);
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Logger()
    {
        Assert.Throws<MArgumentException>(() =>
            new MLog<MLogMethodTests>(null!, _accessorMock.Object, _logContextMock.Object));
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Accessor()
    {
        Assert.Throws<MArgumentException>(() =>
            new MLog<MLogMethodTests>(_loggerMock.Object, null!, _logContextMock.Object));
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_LogContext()
    {
        Assert.Throws<MArgumentException>(() =>
            new MLog<MLogMethodTests>(_loggerMock.Object, _accessorMock.Object, null!));
    }

    [Fact]
    public void Info_With_TraceContext_Should_Record_Trace()
    {
        var sessionMock = new Mock<ITraceSession>();
        sessionMock.Setup(x => x.IsActive).Returns(true);
        var traceCtxMock = new Mock<IMTraceContext>();
        traceCtxMock.Setup(x => x.Current).Returns(sessionMock.Object);

        var sut = new MLog<MLogMethodTests>(
            _loggerMock.Object, _accessorMock.Object, _logContextMock.Object, traceCtxMock.Object);

        sut.Info("hello {name}", "world");

        sessionMock.Verify(
            x => x.Record(It.Is<string>(s => s.Contains("[INFO]")), It.IsAny<object?>()),
            Times.Once);
    }
}

public class MLogContextTests
{
    [Fact]
    public void PushProperty_Should_Return_Disposable_Scope()
    {
        var factory = LoggerFactory.Create(builder => builder.AddDebug());
        var ctx = new MLogContext(factory);

        IMLogContextScope scope = ctx.PushProperty("key", "value");
        Assert.NotNull(scope);
        scope.Dispose(); // should not throw
    }

    [Fact]
    public void PushProperties_Should_Return_Disposable_Scope()
    {
        var factory = LoggerFactory.Create(builder => builder.AddDebug());
        var ctx = new MLogContext(factory);

        IMLogContextScope scope = ctx.PushProperties(new Dictionary<string, object?>
        {
            ["a"] = 1,
            ["b"] = "test"
        });
        Assert.NotNull(scope);
        scope.Dispose();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Factory()
    {
        Assert.Throws<MArgumentException>(() => new MLogContext(null!));
    }
}

public class MLogFactoryTests
{
    [Fact]
    public void CreateLogger_Generic_Should_Return_MLog_Instance()
    {
        var loggerFactory = LoggerFactory.Create(b => { });
        var accessor = new Mock<ISystemExecutionContextAccessor>();
        accessor.Setup(x => x.Get()).Returns(SystemExecutionContext.Empty);
        var logCtx = new MLogContext(loggerFactory);

        var factory = new MLogFactory(loggerFactory, accessor.Object, logCtx);

        IMLog<MLogFactoryTests> logger = factory.CreateLogger<MLogFactoryTests>();
        Assert.NotNull(logger);
    }

    [Fact]
    public void CreateLogger_NonGeneric_Should_Return_Instance()
    {
        var loggerFactory = LoggerFactory.Create(b => { });
        var accessor = new Mock<ISystemExecutionContextAccessor>();
        accessor.Setup(x => x.Get()).Returns(SystemExecutionContext.Empty);
        var logCtx = new MLogContext(loggerFactory);

        var factory = new MLogFactory(loggerFactory, accessor.Object, logCtx);

        IMLog logger = factory.CreateLogger("TestCategory");
        Assert.NotNull(logger);
    }

    [Fact]
    public void Dispose_Should_Not_Throw()
    {
        var loggerFactory = LoggerFactory.Create(b => { });
        var accessor = new Mock<ISystemExecutionContextAccessor>();
        var logCtx = new MLogContext(loggerFactory);

        var factory = new MLogFactory(loggerFactory, accessor.Object, logCtx);
        var ex = Record.Exception(() => factory.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_LoggerFactory()
    {
        Assert.Throws<MArgumentException>(() =>
            new MLogFactory(null!, Mock.Of<ISystemExecutionContextAccessor>(), Mock.Of<IMLogContext>()));
    }
}

public class MLogScopeFactoryTests
{
    [Fact]
    public void BeginScope_Should_Return_Scope()
    {
        var loggerFactory = LoggerFactory.Create(b => { });
        var factory = new MLogScopeFactory(loggerFactory);

        IDisposable? scope = factory.BeginScope(new Dictionary<string, object?> { ["k"] = "v" });
        Assert.NotNull(scope);
        scope?.Dispose();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null()
    {
        Assert.Throws<MArgumentException>(() => new MLogScopeFactory(null!));
    }
}

public class MLogServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMuonroiLogging_Should_Register_All_Required_Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ISystemExecutionContextAccessor>());
        services.AddLogging(builder => builder.AddMuonroiLogging());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMLogContext>());
        Assert.NotNull(provider.GetService<IMLog<MLogServiceCollectionExtensionsTests>>());
        Assert.NotNull(provider.GetService<IMLogFactory>());
        Assert.NotNull(provider.GetService<ILogScopeFactory>());
    }

    [Fact]
    public void AddMuonroiLogging_Should_Throw_On_Null_Builder()
    {
        Assert.Throws<MArgumentException>(() =>
            MLogServiceCollectionExtensions.AddMuonroiLogging(null!));
    }
}
