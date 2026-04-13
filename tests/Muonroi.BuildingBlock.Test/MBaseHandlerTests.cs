using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MBaseHandlerTests
{
    private class TestSink : ILogEventSink
    {
        public readonly List<LogEvent> Events = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private class TestHandler(
        ILogger logger,
        MAuthenticateInfoContext? ctx,
        IMJsonSerializeService json,
        IMDateTimeService date) : MBaseHandler(logger, ctx, json, date)
    {
        public ILogger? ExposedLogger => GetLogger();
        public IMJsonSerializeService? ExposedJson => JsonSerialize;
        public IMDateTimeService? ExposedDate => DateTimeService;
        public MAuthenticateInfoContext? AuthContextPublic => AuthContext;
        public string? CurrentUserGuidPublic => CurrentUserGuid;
        public string? CurrentUsernamePublic => CurrentUsername;

        public void Info(string msg, params object[] args)
        {
            LogInformation(msg, args);
        }

        public void Warn(string msg, params object[] args)
        {
            LogWarning(msg, args);
        }

        public void Error(string msg, params object[] args)
        {
            LogError(msg, args);
        }

        public void DebugMsg(string msg, params object[] args)
        {
            LogDebug(msg, args);
        }
    }

    private static (TestHandler handler, TestSink sink) Create(MAuthenticateInfoContext? ctx = null)
    {
        TestSink sink = new();
        Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        IMJsonSerializeService json = new MJsonSerializeService();
        IMDateTimeService date = new MDateTimeService();
        TestHandler handler = new(logger, ctx, json, date);
        return (handler, sink);
    }

    [Fact]
    public void Logger_Property_Returns_Instance()
    {
        ILogger log = new LoggerConfiguration().CreateLogger();
        TestHandler handler = new(log, new MAuthenticateInfoContext(false), new MJsonSerializeService(),
            new MDateTimeService());
        Assert.Same(log, handler.ExposedLogger);
    }

    [Fact]
    public void JsonSerialize_Property_Returns_Instance()
    {
        IMJsonSerializeService json = new MJsonSerializeService();
        TestHandler handler = new(new LoggerConfiguration().CreateLogger(), new MAuthenticateInfoContext(false), json,
            new MDateTimeService());
        Assert.Same(json, handler.ExposedJson);
    }

    [Fact]
    public void DateTimeService_Property_Returns_Instance()
    {
        IMDateTimeService date = new MDateTimeService();
        TestHandler handler = new(new LoggerConfiguration().CreateLogger(), new MAuthenticateInfoContext(false),
            new MJsonSerializeService(), date);
        Assert.Same(date, handler.ExposedDate);
    }

    [Fact]
    public void Allows_Null_Dependencies()
    {
        TestHandler handler = new(null!, new MAuthenticateInfoContext(false), null!, null!);
        Assert.Null(handler.ExposedLogger);
        Assert.Null(handler.ExposedJson);
        Assert.Null(handler.ExposedDate);
    }

    [Fact]
    public void AuthContext_Returns_Instance()
    {
        MAuthenticateInfoContext ctx = new(false);
        (TestHandler handler, TestSink _) = Create(ctx);
        Assert.Same(ctx, handler.AuthContextPublic);
    }

    [Fact]
    public void AuthContext_Null_Returns_Null()
    {
        (TestHandler handler, TestSink _) = Create(null);
        Assert.Null(handler.AuthContextPublic);
    }

    [Fact]
    public void CurrentUserGuid_Returns_Value()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUserGuid = "guid"
        };
        (TestHandler handler, TestSink _) = Create(ctx);
        Assert.Equal("guid", handler.CurrentUserGuidPublic);
    }

    [Fact]
    public void CurrentUserGuid_Null_Returns_Null()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUserGuid = null!
        };
        (TestHandler handler, TestSink _) = Create(ctx);
        Assert.Null(handler.CurrentUserGuidPublic);
    }

    [Fact]
    public void CurrentUsername_Returns_Value()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUsername = "user"
        };
        (TestHandler handler, TestSink _) = Create(ctx);
        Assert.Equal("user", handler.CurrentUsernamePublic);
    }

    [Fact]
    public void CurrentUsername_Null_Returns_Null()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUsername = null!
        };
        (TestHandler handler, TestSink _) = Create(ctx);
        Assert.Null(handler.CurrentUsernamePublic);
    }

    [Fact]
    public void LogInformation_Writes_Event_With_Format()
    {
        (TestHandler handler, TestSink sink) = Create();
        handler.Info("hello {Name}", "bob");
        LogEvent evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, evt.Level);
        Assert.Equal("hello bob", evt.RenderMessage());
    }

    [Fact]
    public void LogInformation_Null_Message_Throws()
    {
        (TestHandler handler, TestSink _) = Create();
        Assert.Throws<MArgumentException>(() => handler.Info(null!));
    }

    [Fact]
    public void LogWarning_Writes_Event_With_Format()
    {
        (TestHandler handler, TestSink sink) = Create();
        handler.Warn("warn {X}", 1);
        LogEvent evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, evt.Level);
        Assert.Equal("warn 1", evt.RenderMessage());
    }

    [Fact]
    public void LogWarning_Null_Message_Throws()
    {
        (TestHandler handler, TestSink _) = Create();
        Assert.Throws<MArgumentException>(() => handler.Warn(null!));
    }

    [Fact]
    public void LogError_Writes_Event_With_Format()
    {
        (TestHandler handler, TestSink sink) = Create();
        handler.Error("err {Code}", 500);
        LogEvent evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, evt.Level);
        Assert.Equal("err 500", evt.RenderMessage());
    }

    [Fact]
    public void LogError_Null_Message_Throws()
    {
        (TestHandler handler, TestSink _) = Create();
        Assert.Throws<MArgumentException>(() => handler.Error(null!));
    }

    [Fact]
    public void LogDebug_Writes_Event_With_Format()
    {
        (TestHandler handler, TestSink sink) = Create();
        handler.DebugMsg("dbg {Val}", 2);
        LogEvent evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Debug, evt.Level);
        Assert.Equal("dbg 2", evt.RenderMessage());
    }

    [Fact]
    public void LogDebug_Null_Message_Throws()
    {
        (TestHandler handler, TestSink _) = Create();
        Assert.Throws<MArgumentException>(() => handler.DebugMsg(null!));
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        TestHandler handler = new(null!, null!, null!, null!);
        Assert.NotNull(handler);
    }
}
