namespace Muonroi.Mediator.Tests;

public sealed class BaseCommandHandlerTests
{
    [Fact]
    public async Task BaseCommandHandler_ExposesDependencies_AndDelegatesMediatorCalls()
    {
        TestMapper mapper = new();
        TestAuthContext auth = new();
        TestMediator mediator = new();
        TestBaseCommandHandler sut = new(
            mapper,
            auth,
            new TestLog<BaseCommandHandler>(),
            mediator,
            new MPaginationConfig { DefaultPageIndex = 2, DefaultPageSize = 25, MaxPageSize = 100 });

        string sendResult = await sut.SendValueAsync(new TestRequest("payload"));
        await sut.PublishValueAsync(new TestNotification("notice"));

        sendResult.Should().Be("handled:payload");
        mediator.Published.Should().ContainSingle().Which.Message.Should().Be("notice");
        sut.DefaultPageIndexValue.Should().Be(2);
        sut.DefaultPageSizeValue.Should().Be(25);
        sut.MaxPageSizeValue.Should().Be(100);
        sut.CurrentUserIdValue.Should().Be("user-1");
        sut.CurrentUsernameValue.Should().Be("tester");
        sut.MapValue("abc").Should().Be("mapped:abc");
    }

    [Fact]
    public void BaseCommandHandler_MapDestination_WhenMapperReturnsNull_ShouldThrow()
    {
        NullDestinationMapper mapper = new();
        TestBaseCommandHandler sut = new(
            mapper,
            new TestAuthContext(),
            new TestLog<BaseCommandHandler>(),
            new TestMediator(),
            paginationConfig: null);

        Action action = () => sut.MapInto("abc", "target");

        action.Should().Throw<MInternalException>()
            .WithMessage("*Mapping resulted in null*");
    }

    [Fact]
    public async Task MBaseCommandHandler_ExposesContextAndDateServices()
    {
        TestMapper mapper = new();
        TestMediator mediator = new();
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            "tenant-a",
            "user-2",
            "alice",
            "corr-1",
            null,
            null,
            true,
            ["perm.read", "perm.write"],
            "tests"));

        TestMBaseCommandHandler sut = new(
            mapper,
            new TestAuthContext(),
            new TestLog<MBaseCommandHandler>(),
            mediator,
            accessor,
            new FixedDateTimeService(),
            new MPaginationConfig { DefaultPageIndex = 1, DefaultPageSize = 10, MaxPageSize = 50 });

        string sendResult = await sut.SendValueAsync(new TestRequest("hello"));
        await sut.PublishValueAsync(new TestNotification("world"));

        sendResult.Should().Be("handled:hello");
        sut.CurrentTenantIdValue.Should().Be("tenant-a");
        sut.CorrelationIdValue.Should().Be("corr-1");
        sut.CurrentPermissionsValue.Should().Equal("perm.read", "perm.write");
        sut.NowFromService.Should().Be(new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc));
    }

    private sealed record TestRequest(string Value) : IRequest<string>;

    private sealed record TestNotification(string Message) : INotification;

    private sealed class TestBaseCommandHandler(
        IMapper mapper,
        IAuthenticateInfoContext tokenInfo,
        IMLog<BaseCommandHandler> logger,
        IMediator mediator,
        MPaginationConfig? paginationConfig)
        : BaseCommandHandler(mapper, tokenInfo, logger, mediator, paginationConfig)
    {
        public int DefaultPageIndexValue => DefaultPageIndex;
        public int DefaultPageSizeValue => DefaultPageSize;
        public int MaxPageSizeValue => MaxPageSize;
        public string CurrentUserIdValue => CurrentUserId;
        public string CurrentUsernameValue => CurrentUsername;

        public Task<string> SendValueAsync(IRequest<string> request) => SendAsync(request, CancellationToken.None);
        public Task PublishValueAsync(INotification notification) => PublishAsync(notification, CancellationToken.None);
        public string MapValue(object value) => Map<string>(value);
        public string MapInto(object source, string destination) => Map(source, destination);
    }

    private sealed class TestMBaseCommandHandler(
        IMapper mapper,
        IAuthenticateInfoContext tokenInfo,
        IMLog<MBaseCommandHandler> logger,
        IMediator mediator,
        ISystemExecutionContextAccessor contextAccessor,
        IMDateTimeService dateTimeService,
        MPaginationConfig? paginationConfig)
        : MBaseCommandHandler(mapper, tokenInfo, logger, mediator, contextAccessor, dateTimeService, paginationConfig)
    {
        public string? CurrentTenantIdValue => CurrentTenantId;
        public string? CorrelationIdValue => CorrelationId;
        public IReadOnlyList<string> CurrentPermissionsValue => CurrentPermissions;
        public DateTime NowFromService => DateTimeService.UtcNow();

        public Task<string> SendValueAsync(IRequest<string> request) => SendAsync(request, CancellationToken.None);
        public Task PublishValueAsync(INotification notification) => PublishAsync(notification, CancellationToken.None);
    }

    private sealed class TestMediator : IMediator
    {
        public List<TestNotification> Published { get; } = [];

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
        {
            if (notification is TestNotification typed)
            {
                Published.Add(typed);
            }

            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (notification is TestNotification typed)
            {
                Published.Add(typed);
            }

            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is TestRequest typed)
            {
                return Task.FromResult((TResponse)(object)$"handled:{typed.Value}");
            }

            throw new InvalidOperationException("Unexpected request type.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(request is TestRequest typed ? $"handled:{typed.Value}" : null);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestMapper : IMapper
    {
        public T Map<T>(object source) => (T)(object)$"mapped:{source}";

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
            => (TDestination)(object)$"mapped-into:{source}";

        public object Map(object source, object destination) => destination switch
        {
            string => $"mapped-into:{source}",
            _ => destination
        };
    }

    private sealed class NullDestinationMapper : IMapper
    {
        public T Map<T>(object source) => throw new NotSupportedException();

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
            => throw new InvalidOperationException("Expected object-based mapping path.");

        public object Map(object source, object destination) => null!;
    }

    private sealed class TestAuthContext : IAuthenticateInfoContext
    {
        public string CorrelationId { get; set; } = "corr-1";
        public string CurrentUserGuid { get; set; } = "user-1";
        public string CurrentUsername { get; set; } = "tester";
        public string? TenantId { get; set; } = "tenant-a";
        public string TokenValidityKey { get; set; } = "valid";
        public string? AccessToken { get; set; } = "token";
        public string? ApiKey { get; set; } = "api-key";
        public string? Permission { get; set; } = "perm.read";
        public string Language { get; set; } = "vi";
        public string Caller { get; set; } = "tests";
        public MUserModel? CurrentUser { get; set; }
        public bool IsAuthenticated { get; set; } = true;

        public string GetAccessToken() => AccessToken ?? string.Empty;
    }

    private sealed class FixedDateTimeService : IMDateTimeService
    {
        private static readonly DateTime FixedUtc = new(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        public DateTime Now() => FixedUtc;
        public DateTime UtcNow() => FixedUtc;
        public DateTime Today() => FixedUtc.Date;
        public DateTime UtcToday() => FixedUtc.Date;
        public double NowTs() => 1_893_554_645d;
        public double UtcNowTs() => 1_893_554_645d;
    }

    private sealed class TestLog<T> : IMLog<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        public IMLogContextScope BeginProperty(string key, object? value) => new NullScope();
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warn(string messageTemplate, params object?[] args) { }
        public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
        public void Debug(string messageTemplate, params object?[] args) { }
        public void InfoTrace(string messageTemplate, params object?[] args) { }
        public void InfoContext(string messageTemplate, params object?[] args) { }
        public void InfoContext(string messageTemplate, object? arg0 = null, object? arg1 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }
        public void ErrorContext(Exception? ex, string messageTemplate, params object?[] args) { }
        public void ErrorContext(Exception? ex, string messageTemplate, object? arg0 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }
        public void Audit(string messageTemplate, params object?[] args) { }
        public void Audit(string messageTemplate, string? auditType = null, string? action = null, bool isSuccess = true, string? targetId = null, string? targetType = null, object? metadata = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }
    }

    private sealed class NullScope : IMLogContextScope
    {
        public void Dispose() { }
    }
}
