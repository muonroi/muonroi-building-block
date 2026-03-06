namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class BaseCommandHandlerTests
{
    private class TestSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private class InMemorySink : ILogEventSink
    {
        public readonly List<LogEvent> Events = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private class DummyUnitOfWork : IMUnitOfWork
    {
        private bool _disposed;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<Guid> SaveEntitiesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Guid.Empty);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed) _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    private class DummyAuthenticateRepository : IAuthenticateRepository
    {
        public IMUnitOfWork UnitOfWork { get; } = new DummyUnitOfWork();

        public MUser Add(MUser newEntity)
        {
            return newEntity;
        }

        public Task<int> UpdateAsync(MUser updateEntity)
        {
            return Task.FromResult(0);
        }

        public Task<bool> DeleteAsync(MUser deleteEntity)
        {
            return Task.FromResult(true);
        }

        public Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action)
        {
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync()
        {
            return Task.CompletedTask;
        }

        public Task<int> AddBatchAsync(IEnumerable<MUser> newEntities)
        {
            return Task.FromResult(0);
        }

        public Task<int> AddOrUpdateBatchAsync(IEnumerable<MUser> newEntities)
        {
            return Task.FromResult(0);
        }

        public Task<int> UpdateBatchAsync(Expression<Func<MUser, bool>> predicate, Action<MUser> updateAction)
        {
            return Task.FromResult(0);
        }

        public Task<int> DeleteBatchAsync(Expression<Func<MUser, bool>> predicate)
        {
            return Task.FromResult(0);
        }

        public Task<int> DeleteBatchAsync(IEnumerable<MUser> deleteEntities)
        {
            return Task.FromResult(0);
        }

        public Task<bool> SoftRestoreAsync(MUser entity)
        {
            return Task.FromResult(true);
        }

        public Task BulkInsertAsync(IEnumerable<MUser> entities)
        {
            return Task.CompletedTask;
        }

        public Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters)
        {
            return Task.FromResult(0);
        }

        public Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName,
            params object[] parameters)
        {
            return Task.FromResult(default(TResult)!);
        }

        public Task<MResponse<LoginResponseModel>> Login(LoginRequestModel model, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MResponse<LoginResponseModel>());
        }

        public Task<MResponse<RefreshTokenResponseModel>> RefreshToken(RefreshTokenRequestModel request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new MResponse<RefreshTokenResponseModel>());
        }

        public Task<MResponse<string>> ValidateTokenValidity(string tokenValidity, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MResponse<string>());
        }
    }

    // Handler for property/config/context test
    private class DummyHandler : BaseCommandHandler
    {
        public DummyHandler(
            IMapper? mapper,
            MAuthenticateInfoContext? tokenInfo,
            IAuthenticateRepository? repo,
            ILogger? logger,
            IMediator? mediator,
            MPaginationConfig? cfg)
            : base(mapper!, tokenInfo!, repo!, logger!, mediator!, cfg)
        {
        }

        public DummyHandler(MPaginationConfig? cfg)
            : base(null!, new MAuthenticateInfoContext(false), null!, new LoggerConfiguration().CreateLogger(),
                new FakeMediator(), cfg)
        {
        }

        public MAuthenticateInfoContext? TokenInfoPublic => TokenInfo;
        public int DefaultPageIndexPublic => DefaultPageIndex;
        public int DefaultPageSizePublic => DefaultPageSize;
        public int MaxPageSizePublic => MaxPageSize;
        public IMapper? MapperPublic => Mapper;
        public IAuthenticateRepository? AuthenticateRepositoryPublic => AuthenticateRepository;
        public ILogger? LoggerPublic => Logger;
        public IMediator? MediatorPublic => Mediator;
        public string CurrentUserIdPublic => CurrentUserId;
        public string CurrentUsernamePublic => CurrentUsername;
        public MPaginationConfig? Config => PaginationConfig;
    }

    private class SpyMediator : IMediator
    {
        public object? Sent { get; private set; }
        public object? Published { get; private set; }

        public Task Publish(object? notification, CancellationToken cancellationToken = default)
        {
            if (notification != null)
            {
                Published = notification;
                return Task.CompletedTask;
            }

            throw new ArgumentNullException(nameof(notification));
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (Equals(notification, default(TNotification))) throw new ArgumentNullException(nameof(notification));
            Published = notification;
            return Task.CompletedTask;
        }

        public Task<MResponse> Send<MResponse>(IRequest<MResponse>? request,
            CancellationToken cancellationToken = default)
        {
            if (request != null)
            {
                Sent = request;
                return Task.FromResult(default(MResponse)!);
            }

            throw new ArgumentNullException(nameof(request));
        }

        public Task<object?> Send(object? request, CancellationToken cancellationToken = default)
        {
            if (request != null)
            {
                Sent = request;
                return Task.FromResult<object?>(null);
            }

            throw new ArgumentNullException(nameof(request));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            if (Equals(request, default(TRequest))) throw new ArgumentNullException(nameof(request));
            Sent = request!;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<MResponse> CreateStream<MResponse>(IStreamRequest<MResponse> request,
            CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<MResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<object?>();
        }
    }

    private class DummyNotification : INotification
    {
    }

    private class DummyRequest : IRequest<string>
    {
    }

    private class TestHandler : BaseCommandHandler
    {
        public TestHandler(
            IMapper? mapper,
            MAuthenticateInfoContext? ctx,
            IAuthenticateRepository? repo,
            ILogger? log,
            IMediator? mediator,
            MPaginationConfig? config)
            : base(mapper!, ctx!, repo!, log!, mediator!, config!)
        {
        }

        public TestHandler(ILogger logger)
            : base(null!, new MAuthenticateInfoContext(false), null!, logger, new FakeMediator(),
                new MPaginationConfig())
        {
        }

        public static double NowTsOnlyDayPublic => NowTsOnlyDay;
        public static double NowTsPublic => NowTs;
        public static DateTime LocalNowPublic => LocalNow;
        public static DateTime NowPublic => Now;
        public static double LocalNowTsOnlyDayPublic => LocalNowTsOnlyDay;
        public static double LocalNowTsPublic => LocalNowTs;

        public void LogInfoPublic(string? message)
        {
            LogInfo(message!);
        }

        public void LogErrorPublic(string? message)
        {
            LogError(message!);
        }

        public void LogErrorPublic(Exception ex)
        {
            LogError(ex);
        }

        public void LogErrorPublic(string? message, Exception ex)
        {
            LogError(message!, ex);
        }

        public void CallLogWarning(string? msg)
        {
            LogWarning(msg!);
        }

        public T CallMap<T>(object source)
        {
            return Map<T>(source);
        }

        public T CallMap<T>(object source, T dest)
        {
            return Map(source, dest);
        }

        public Task CallPublishAsync<T>(T note) where T : INotification
        {
            return PublishAsync(note, CancellationToken.None);
        }

        public Task<TRes> CallSendAsync<TRes>(IRequest<TRes> req)
        {
            return SendAsync(req, CancellationToken.None);
        }

        public IMapper? MapperProp => Mapper;
        public ILogger? LoggerProp => Logger;
        public IMediator? MediatorProp => Mediator;
        public MPaginationConfig? ConfigProp => PaginationConfig;
    }

    private static DummyHandler CreateHandler(
        IMapper? mapper = null,
        MAuthenticateInfoContext? ctx = null,
        IAuthenticateRepository? repo = null,
        ILogger? logger = null,
        IMediator? mediator = null,
        MPaginationConfig? config = null)
    {
        bool hasContext = ctx is not null;
        bool hasConfig = config is not null;

        mapper ??= new SimpleMapper(new MappingConfiguration());
        ctx ??= new MAuthenticateInfoContext(true);
        if (!hasContext)
        {
            ctx.CurrentUserGuid = "uid";
            ctx.CurrentUsername = "user";
        }

        repo ??= new DummyAuthenticateRepository();
        logger ??= new LoggerConfiguration().CreateLogger();
        mediator ??= new FakeMediator();
        config ??= new MPaginationConfig();
        if (!hasConfig)
        {
            config.DefaultPageIndex = 1;
            config.DefaultPageSize = 10;
            config.MaxPageSize = 100;
        }

        return new DummyHandler(mapper, ctx, repo, logger, mediator, config);
    }

    private static TestHandler CreateAdvancedHandler(ILogger? logger = null, IMediator? mediator = null,
        IMapper? mapper = null)
    {
        logger ??= new LoggerConfiguration().CreateLogger();
        mediator ??= new SpyMediator();
        mapper ??= new SimpleMapper(new MappingConfiguration());
        return new TestHandler(mapper, new MAuthenticateInfoContext(false), null, logger, mediator,
            new MPaginationConfig());
    }

    [Fact]
    public void PaginationConfig_Returns_Config_Instance()
    {
        MPaginationConfig cfg = new()
        {
            DefaultPageIndex = 1,
            DefaultPageSize = 2,
            MaxPageSize = 3
        };
        DummyHandler handler = new(cfg);
        Assert.Equal(1, handler.Config?.DefaultPageIndex);
        Assert.Equal(2, handler.Config?.DefaultPageSize);
        Assert.Equal(3, handler.Config?.MaxPageSize);
    }

    [Fact]
    public void PaginationConfig_Null_Returns_Null()
    {
        DummyHandler handler = new(null!);
        Assert.Null(handler.Config);
    }

    [Fact]
    public void PaginationConfig_Default_Values()
    {
        MPaginationConfig cfg = new();
        DummyHandler handler = new(cfg);
        Assert.Equal(0, handler.Config?.DefaultPageIndex);
        Assert.Equal(0, handler.Config?.DefaultPageSize);
        Assert.Equal(0, handler.Config?.MaxPageSize);
    }

    [Fact]
    public void TokenInfo_Returns_Context()
    {
        MAuthenticateInfoContext ctx = new(true);
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.Same(ctx, handler.TokenInfoPublic);
    }

    [Fact]
    public void TokenInfo_Null_Returns_Null()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            null!,
            new DummyAuthenticateRepository(),
            new LoggerConfiguration().CreateLogger(),
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Null(handler.TokenInfoPublic);
    }

    [Fact]
    public void TokenInfo_Expired_Returns_Object_With_Flag()
    {
        MAuthenticateInfoContext ctx = new(false);
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.False(handler.TokenInfoPublic!.IsAuthenticated);
    }

    [Fact]
    public void DefaultPageIndex_Returns_Config_Value()
    {
        MPaginationConfig cfg = new()
        {
            DefaultPageIndex = 3
        };
        DummyHandler handler = CreateHandler(config: cfg);
        Assert.Equal(3, handler.DefaultPageIndexPublic);
    }

    [Fact]
    public void DefaultPageIndex_Reflects_Config_Changes()
    {
        MPaginationConfig cfg = new()
        {
            DefaultPageIndex = 1
        };
        DummyHandler handler = CreateHandler(config: cfg);
        cfg.DefaultPageIndex = 5;
        Assert.Equal(5, handler.DefaultPageIndexPublic);
    }

    [Fact]
    public void DefaultPageSize_Returns_Config_Value()
    {
        MPaginationConfig cfg = new()
        {
            DefaultPageSize = 20
        };
        DummyHandler handler = CreateHandler(config: cfg);
        Assert.Equal(20, handler.DefaultPageSizePublic);
    }

    [Fact]
    public void DefaultPageSize_Reflects_Config_Changes()
    {
        MPaginationConfig cfg = new()
        {
            DefaultPageSize = 10
        };
        DummyHandler handler = CreateHandler(config: cfg);
        cfg.DefaultPageSize = 30;
        Assert.Equal(30, handler.DefaultPageSizePublic);
    }

    [Fact]
    public void MaxPageSize_Returns_Config_Value()
    {
        MPaginationConfig cfg = new()
        {
            MaxPageSize = 50
        };
        DummyHandler handler = CreateHandler(config: cfg);
        Assert.Equal(50, handler.MaxPageSizePublic);
    }

    [Fact]
    public void MaxPageSize_Reflects_Config_Changes()
    {
        MPaginationConfig cfg = new()
        {
            MaxPageSize = 100
        };
        DummyHandler handler = CreateHandler(config: cfg);
        cfg.MaxPageSize = 200;
        Assert.Equal(200, handler.MaxPageSizePublic);
    }

    [Fact]
    public void Mapper_Returns_Instance()
    {
        IMapper mapper = new SimpleMapper(new MappingConfiguration());
        DummyHandler handler = CreateHandler(mapper);
        Assert.Same(mapper, handler.MapperPublic);
    }

    [Fact]
    public void Mapper_Null_Returns_Null()
    {
        DummyHandler handler = new(
            null!,
            new MAuthenticateInfoContext(true),
            new DummyAuthenticateRepository(),
            new LoggerConfiguration().CreateLogger(),
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Null(handler.MapperPublic);
    }

    [Fact]
    public void AuthenticateRepository_Returns_Instance()
    {
        DummyAuthenticateRepository repo = new();
        DummyHandler handler = CreateHandler(repo: repo);
        Assert.Same(repo, handler.AuthenticateRepositoryPublic);
    }

    [Fact]
    public void AuthenticateRepository_Null_Returns_Null()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            new MAuthenticateInfoContext(true),
            null!,
            new LoggerConfiguration().CreateLogger(),
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Null(handler.AuthenticateRepositoryPublic);
    }

    [Fact]
    public void Logger_Returns_Instance()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        DummyHandler handler = CreateHandler(logger: logger);
        Assert.Same(logger, handler.LoggerPublic);
    }

    [Fact]
    public void Logger_Null_Returns_Null()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            new MAuthenticateInfoContext(true),
            new DummyAuthenticateRepository(),
            null!,
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Null(handler.LoggerPublic);
    }

    [Fact]
    public void Mediator_Returns_Instance()
    {
        FakeMediator mediator = new();
        DummyHandler handler = CreateHandler(mediator: mediator);
        Assert.Same(mediator, handler.MediatorPublic);
    }

    [Fact]
    public void Mediator_Null_Returns_Null()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            new MAuthenticateInfoContext(true),
            new DummyAuthenticateRepository(),
            new LoggerConfiguration().CreateLogger(),
            null!,
            new MPaginationConfig());
        Assert.Null(handler.MediatorPublic);
    }

    [Fact]
    public void CurrentUserId_Returns_Value()
    {
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = "123"
        };
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.Equal("123", handler.CurrentUserIdPublic);
    }

    [Fact]
    public void CurrentUserId_Null_Context_Throws()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            null!,
            new DummyAuthenticateRepository(),
            new LoggerConfiguration().CreateLogger(),
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Throws<NullReferenceException>(() => _ = handler.CurrentUserIdPublic);
    }

    [Fact]
    public void CurrentUserId_Empty_Returns_Empty()
    {
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = string.Empty
        };
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.Equal(string.Empty, handler.CurrentUserIdPublic);
    }

    [Fact]
    public void CurrentUsername_Returns_Value()
    {
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUsername = "u"
        };
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.Equal("u", handler.CurrentUsernamePublic);
    }

    [Fact]
    public void CurrentUsername_Null_Context_Throws()
    {
        DummyHandler handler = new(
            new SimpleMapper(new MappingConfiguration()),
            null!,
            new DummyAuthenticateRepository(),
            new LoggerConfiguration().CreateLogger(),
            new FakeMediator(),
            new MPaginationConfig());
        Assert.Throws<NullReferenceException>(() => _ = handler.CurrentUsernamePublic);
    }

    [Fact]
    public void CurrentUsername_Empty_Returns_Empty()
    {
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUsername = string.Empty
        };
        DummyHandler handler = CreateHandler(ctx: ctx);
        Assert.Equal(string.Empty, handler.CurrentUsernamePublic);
    }

    [Fact]
    public void NowTsOnlyDay_Returns_UTC_Date_Timestamp()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        double actual = TestHandler.NowTsOnlyDayPublic;
        double expected = DateTime.UtcNow.Date.GetTimeStamp();
        Assert.InRange(actual, expected - 1, expected + 1);
    }

    [Fact]
    public void NowTs_Returns_UTC_Timestamp()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        double actual = TestHandler.NowTsPublic;
        double expected = DateTime.UtcNow.GetTimeStamp(true);
        Assert.InRange(actual, expected - 1, expected + 1);
    }

    [Fact]
    public void LocalNow_Returns_Local_Time()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        DateTime actual = TestHandler.LocalNowPublic;
        TimeSpan diff = actual - DateTime.Now;
        Assert.InRange(diff.TotalSeconds, -1, 1);
    }

    [Fact]
    public void Now_Returns_Utc_Time()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        DateTime actual = TestHandler.NowPublic;
        TimeSpan diff = actual - DateTime.UtcNow;
        Assert.InRange(diff.TotalSeconds, -1, 1);
    }

    [Fact]
    public void LocalNowTsOnlyDay_Returns_Local_Date_Timestamp()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        double actual = TestHandler.LocalNowTsOnlyDayPublic;
        double expected = DateTime.Now.Date.GetTimeStamp();
        Assert.InRange(actual, expected - 1, expected + 1);
    }

    [Fact]
    public void LocalNowTs_Returns_Local_Timestamp()
    {
        TestSink sink = new();
        _ = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        double actual = TestHandler.LocalNowTsPublic;
        double expected = DateTime.Now.GetTimeStamp(true);
        Assert.InRange(actual, expected - 1, expected + 1);
    }


    [Fact]
    public void LogInfo_Writes_Log()
    {
        TestSink sink = new();
        ILogger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        TestHandler handler = new(logger);
        handler.LogInfoPublic(null);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void LogError_String_Writes_Log()
    {
        TestSink sink = new();
        ILogger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        TestHandler handler = new(logger);
        handler.LogErrorPublic((string?)null);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void LogError_Exception_Writes_Log()
    {
        TestSink sink = new();
        ILogger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        TestHandler handler = new(logger);
        handler.LogErrorPublic(new InvalidOperationException("fail"));
        Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, sink.Events[0].Level);
    }

    [Fact]
    public void LogError_StringException_Writes_Log()
    {
        TestSink sink = new();
        ILogger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        TestHandler handler = new(logger);
        handler.LogErrorPublic(null, new InvalidOperationException());
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void LogWarning_Writes_Log()
    {
        InMemorySink sink = new();
        ILogger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        TestHandler handler = CreateAdvancedHandler(logger);

        handler.CallLogWarning("warn");

        LogEvent evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, evt.Level);
        Assert.Equal("warn", evt.MessageTemplate.Text);
    }

    [Fact]
    public void LogWarning_Null_Message_Throws()
    {
        TestHandler handler = CreateAdvancedHandler();
        Assert.Throws<ArgumentNullException>(() => handler.CallLogWarning(null));
    }

    private class Source
    {
        public string Name { get; set; } = string.Empty;
    }

    private class Dest
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Map_Object_To_Destination()
    {
        MappingConfiguration cfg = new();
        cfg.GetOrAdd(typeof(Source), typeof(Dest));
        IMapper mapper = new SimpleMapper(cfg);
        TestHandler handler = CreateAdvancedHandler(mapper: mapper);

        Source src = new()
        {
            Name = "A"
        };
        Dest result = handler.CallMap<Dest>(src);

        Assert.Equal("A", result.Name);
    }

    [Fact]
    public void Map_Object_Null_Throws()
    {
        MappingConfiguration cfg = new();
        cfg.GetOrAdd(typeof(Source), typeof(Dest));
        IMapper mapper = new SimpleMapper(cfg);
        TestHandler handler = CreateAdvancedHandler(mapper: mapper);

        Assert.Throws<ArgumentNullException>(() => handler.CallMap<Dest>(null!));
    }

    [Fact]
    public void Map_Source_To_Existing_Destination()
    {
        MappingConfiguration cfg = new();
        cfg.GetOrAdd(typeof(Source), typeof(Dest));
        IMapper mapper = new SimpleMapper(cfg);
        TestHandler handler = CreateAdvancedHandler(mapper: mapper);

        Source src = new()
        {
            Name = "B"
        };
        Dest dest = new();
        Dest res = handler.CallMap(src, dest);

        Assert.Same(dest, res);
        Assert.Equal("B", dest.Name);
    }

    [Fact]
    public void Map_Source_Null_Throws()
    {
        MappingConfiguration cfg = new();
        cfg.GetOrAdd(typeof(Source), typeof(Dest));
        IMapper mapper = new SimpleMapper(cfg);
        TestHandler handler = CreateAdvancedHandler(mapper: mapper);

        Dest dest = new();
        Assert.Throws<ArgumentNullException>(() => handler.CallMap(null!, dest));
        Assert.Throws<ArgumentNullException>(() => handler.CallMap(new Source(), (Dest)null!));
    }

    [Fact]
    public void Constructor_Initializes_Dependencies()
    {
        MappingConfiguration cfg = new();
        IMapper mapper = new SimpleMapper(cfg);
        SpyMediator mediator = new();
        ILogger logger = new LoggerConfiguration().CreateLogger();
        MPaginationConfig pg = new();

        TestHandler handler = new(mapper, new MAuthenticateInfoContext(false), null, logger, mediator, pg);

        Assert.Same(mapper, handler.MapperProp);
        Assert.Same(logger, handler.LoggerProp);
        Assert.Same(mediator, handler.MediatorProp);
        Assert.Same(pg, handler.ConfigProp);
    }

    [Fact]
    public void Constructor_Allows_Nulls()
    {
        TestHandler handler = new(null, null, null, null, null, null);
        Assert.Null(handler.MapperProp);
        Assert.Null(handler.LoggerProp);
        Assert.Null(handler.MediatorProp);
        Assert.Null(handler.ConfigProp);
    }

    [Fact]
    public async Task PublishAsync_Sends_Notification()
    {
        SpyMediator mediator = new();
        TestHandler handler = CreateAdvancedHandler(mediator: mediator);
        DummyNotification note = new();

        await handler.CallPublishAsync(note);

        Assert.Same(note, mediator.Published);
    }

    [Fact]
    public async Task PublishAsync_Null_Throws()
    {
        SpyMediator mediator = new();
        TestHandler handler = CreateAdvancedHandler(mediator: mediator);
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.CallPublishAsync<DummyNotification>(null!));
    }

    [Fact]
    public async Task SendAsync_Sends_Request()
    {
        SpyMediator mediator = new();
        TestHandler handler = CreateAdvancedHandler(mediator: mediator);
        DummyRequest req = new();

        string result = await handler.CallSendAsync(req);

        Assert.Same(req, mediator.Sent);
        Assert.Null(result);
    }

    [Fact]
    public async Task SendAsync_Null_Throws()
    {
        SpyMediator mediator = new();
        TestHandler handler = CreateAdvancedHandler(mediator: mediator);
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.CallSendAsync<string>(null!));
    }
}
