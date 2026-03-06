namespace Muonroi.BuildingBlock.Test;

public class MSerilogActionTests
{
    private static MethodInfo GetPrivate(string name)
    {
        return typeof(MSerilogAction).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    [Fact]
    public void Configure_Adds_Console_Sink()
    {
        HostBuilderContext ctx = new(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().Build(),
            HostingEnvironment = Substitute.For<IHostEnvironment>()
        };
        ServiceCollection services = [];
        services.AddSingleton<IEnumerable<ILoggerSettings>>([]);
        IServiceProvider sp = services.BuildServiceProvider();
        LoggerConfiguration lc = new();
        MSerilogAction.Configure(ctx, sp, lc);
        using Logger log = lc.CreateLogger();
        Assert.NotNull(log);
    }

    [Fact]
    public void Configure_Respects_Minimum_Level_From_Configuration()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Serilog:MinimumLevel:Default"] = "Debug"
        };
        HostBuilderContext ctx = new(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            HostingEnvironment = Substitute.For<IHostEnvironment>()
        };
        ServiceCollection services = [];
        services.AddSingleton<IEnumerable<ILoggerSettings>>([]);
        IServiceProvider provider = services.BuildServiceProvider();
        LoggerConfiguration loggerConfiguration = new();

        MSerilogAction.Configure(ctx, provider, loggerConfiguration, false);

        CapturingSink sink = new();
        _ = loggerConfiguration.WriteTo.Sink(sink);

        using (Logger logger = loggerConfiguration.CreateLogger())
        {
            logger.Debug("debug message");
            logger.Information("info message");
        }

        Assert.Contains(sink.Events, e => e is { Level: LogEventLevel.Debug, MessageTemplate.Text: "debug message" });
        Assert.Contains(sink.Events,
            e => e is { Level: LogEventLevel.Information, MessageTemplate.Text: "info message" });
    }

    [Fact]
    public void Configure_Null_Configuration_Throws()
    {
        HostBuilderContext ctx = new(new Dictionary<object, object>());
        LoggerConfiguration lc = new();
        Assert.ThrowsAny<Exception>(() => MSerilogAction.Configure(ctx, Substitute.For<IServiceProvider>(), lc));
    }

    [Fact]
    public void Configure_Null_Context_Throws()
    {
        LoggerConfiguration lc = new();
        Assert.ThrowsAny<Exception>(() => MSerilogAction.Configure(null!, Substitute.For<IServiceProvider>(), lc));
    }

    [Fact]
    public void AddElasticsearchSink_Adds_Sink_With_Valid_Config()
    {
        MethodInfo mi = GetPrivate("AddElasticsearchSink");
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:Elasticsearch:nodes:0"] = "http://localhost:9200"
            }).Build();
        LoggerConfiguration lc = new();
        mi.Invoke(null, [cfg, lc]);
        using Logger log = lc.CreateLogger();
        Assert.NotNull(log);
    }

    [Fact]
    public void AddElasticsearchSink_Invalid_Config_Does_Nothing()
    {
        MethodInfo mi = GetPrivate("AddElasticsearchSink");
        IConfiguration cfg = new ConfigurationBuilder().Build();
        LoggerConfiguration lc = new();
        mi.Invoke(null, [cfg, lc]);
        using Logger log = lc.CreateLogger();
        Assert.NotNull(log);
    }

    [Fact]
    public void AddElasticsearchSink_Invalid_Uri_Throws()
    {
        MethodInfo mi = GetPrivate("AddElasticsearchSink");
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:Elasticsearch:nodes:0"] = "not a uri"
            }).Build();
        LoggerConfiguration lc = new();
        Assert.ThrowsAny<Exception>(() => mi.Invoke(null, [cfg, lc]));
    }

    [Fact]
    public void AddFileSink_Writes_To_File()
    {
        MethodInfo mi = GetPrivate("AddFileSink");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:File:Path"] = path,
                ["Serilog:File:RollingInterval"] = "Day"
            }).Build();
        LoggerConfiguration lc = new();
        mi.Invoke(null, [cfg, lc]);
        using (Logger log = lc.CreateLogger())
        {
            log.Information("test");
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (!File.Exists(path) && sw.ElapsedMilliseconds < 2000) Thread.Yield();
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AddFileSink_Empty_Path_Does_Not_Create_File()
    {
        MethodInfo mi = GetPrivate("AddFileSink");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
        IConfiguration cfg = new ConfigurationBuilder().Build();
        LoggerConfiguration lc = new();
        mi.Invoke(null, [cfg, lc]);
        using (Logger log = lc.CreateLogger())
        {
            log.Information("none");
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (!File.Exists(path) && sw.ElapsedMilliseconds < 2000) Thread.Yield();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void AddFileSink_Invalid_Path_Throws()
    {
        MethodInfo mi = GetPrivate("AddFileSink");
        string tempFile = Path.GetTempFileName();
        string invalidPath = Path.Combine(tempFile, "test.log");
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:File:Path"] = invalidPath
            }).Build();
        LoggerConfiguration lc = new();
        Assert.ThrowsAny<Exception>(() =>
        {
            mi.Invoke(null, [cfg, lc]);
            using Logger log = lc.CreateLogger();
            log.Information("fail");
        });
    }

    [Fact]
    public void GetBool_Parses_Value()
    {
        MethodInfo mi = GetPrivate("GetBool");
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["flag"] = "true"
            }).Build();
        bool result = (bool)mi.Invoke(null, [cfg, "flag", false])!;
        Assert.True(result);
    }

    [Fact]
    public void GetBool_Returns_Default_When_Invalid()
    {
        MethodInfo mi = GetPrivate("GetBool");
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["flag"] = "invalid"
        }).Build();
        bool result = (bool)mi.Invoke(null, [cfg, "flag", true])!;
        Assert.True(result);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
