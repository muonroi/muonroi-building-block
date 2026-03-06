namespace Muonroi.Mediator.Tests;

public class MediatorTests
{
    public class PingRequest : IRequest<string>
    {
        public string? Message { get; set; }
    }

    public class PingHandler : IRequestHandler<PingRequest, string>
    {
        public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Pong{request.Message}");
        }
    }

    public class LoggingBehavior<TRequest, MResponse>(IList<string> log) : IPipelineBehavior<TRequest, MResponse>
        where TRequest : IRequest<MResponse>
    {
        public async Task<MResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<MResponse> next,
            CancellationToken cancellationToken)
        {
            log.Add("before");
            MResponse response = await next();
            log.Add("after");
            return response;
        }
    }

    public class TestNotification(string message) : INotification
    {
        public string Message { get; } = message;
    }

    public class NotificationHandler : INotificationHandler<TestNotification>
    {
        public static List<string> Received { get; } = [];

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            Received.Add(notification.Message);
            return Task.CompletedTask;
        }
    }

    public class NumberStreamRequest(int count) : IStreamRequest<int>
    {
        public int Count { get; } = count;
    }

    public class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            NumberStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }

    private static readonly string[] Expected = ["before", "after"];

    [Fact]
    public async Task Send_Returns_Handler_Result()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddMediator(typeof(MediatorTests).Assembly);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        PingRequest request = new()
        {
            Message = "!"
        };

        string result = await mediator.Send(request);

        Assert.Equal("Pong!", result);
    }

    [Fact]
    public async Task PipelineBehavior_Invoked()
    {
        ServiceCollection services = [];
        List<string> log = [];
        services.AddSingleton<IList<string>>(log);
        services.AddMediator(typeof(MediatorTests).Assembly);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new PingRequest());

        Assert.Equal(Expected, log);
    }

    [Fact]
    public async Task Publish_Calls_NotificationHandler()
    {
        NotificationHandler.Received.Clear();

        ServiceCollection services = [];
        services.AddMediator(typeof(MediatorTests).Assembly);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        await mediator.Publish(new TestNotification("hello"));

        Assert.Contains("hello", NotificationHandler.Received);
    }

    [Fact]
    public async Task CreateStream_Returns_IAsyncEnumerable()
    {
        ServiceCollection services = [];
        services.AddMediator(typeof(MediatorTests).Assembly);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        List<int> result = [];
        await foreach (int item in mediator.CreateStream(new NumberStreamRequest(3)))
        {
            result.Add(item);
        }

        Assert.Equal([0, 1, 2], [.. result]);
    }
}
