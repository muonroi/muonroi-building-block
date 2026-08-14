namespace Muonroi.Mediator.Tests;

public class MediatorAdditionalTests
{
    private class ThrowingHandler : IRequestHandler<MediatorTests.PingRequest, string>
    {
        public Task<string> Handle(MediatorTests.PingRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("fail");
        }
    }

    [Fact]
    public async Task Send_Handler_Throws_Propagates()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddMediator(typeof(MediatorTests).Assembly);
        services.AddTransient<IRequestHandler<MediatorTests.PingRequest, string>, ThrowingHandler>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        // v2: no TargetInvocationException wrapping — exception propagates directly (no reflection on hot path)
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new MediatorTests.PingRequest()));
    }

    [Fact]
    public async Task SendDynamic_Returns_Result()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddMediator(typeof(MediatorTests).Assembly);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        MediatorTests.PingRequest request = new()
        {
            Message = "!"
        };

        object? result = await mediator.Send(request);

        Assert.Equal("Pong!", result);
    }

    [Fact]
    public async Task SendDynamic_No_Handler_Throws()
    {
        MMediator mediator = new(_ => null);

        await Assert.ThrowsAsync<MInternalException>(() => mediator.Send(new MediatorTests.PingRequest()));
    }

    [Fact]
    public async Task SendDynamic_Wrong_Type_Throws()
    {
        MMediator mediator = new(_ => null);

        await Assert.ThrowsAsync<MInternalException>(() => mediator.Send(new object()));
    }

    private class PingVoid : IRequest
    {
    }

    private class PingVoidHandler : IRequestHandler<PingVoid, Unit>
    {
        public static bool Called;

        public Task<Unit> Handle(PingVoid request, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(Unit.Value);
        }
    }

    [Fact]
    public async Task Send_NonGeneric_Request_Works()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddMediator(typeof(MediatorTests).Assembly, typeof(MediatorAdditionalTests).Assembly);
        services.AddTransient<IRequestHandler<PingVoid, Unit>, PingVoidHandler>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new PingVoid());

        Assert.True(PingVoidHandler.Called);
    }

    private class ThrowingStreamRequest : IStreamRequest<int>
    {
    }

    private class ThrowingStreamHandler : IStreamRequestHandler<ThrowingStreamRequest, int>
    {
#pragma warning disable CS0162
        public async IAsyncEnumerable<int> Handle(
            ThrowingStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("fail");
            yield break;
        }
#pragma warning restore CS0162
    }

    [Fact]
    public async Task CreateStream_Handler_Throws_Propagates()
    {
        ServiceCollection services = [];
        services.AddMediator(typeof(MediatorTests).Assembly, typeof(MediatorAdditionalTests).Assembly);
        services.AddTransient<IStreamRequestHandler<ThrowingStreamRequest, int>, ThrowingStreamHandler>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (int _ in mediator.CreateStream(new ThrowingStreamRequest()))
            {
            }
        });
    }

    private class ThrowingHandlerDynamic : IRequestHandler<MediatorTests.PingRequest, string>
    {
        public Task<string> Handle(MediatorTests.PingRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("fail");
        }
    }

    [Fact]
    public async Task SendDynamic_Handler_Throws_Propagates()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddMediator(typeof(MediatorTests).Assembly);
        services.AddTransient<IRequestHandler<MediatorTests.PingRequest, string>, ThrowingHandlerDynamic>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IMediator mediator = serviceProvider.GetRequiredService<IMediator>();

        // v2: no TargetInvocationException wrapping — exception propagates directly
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send((object)new MediatorTests.PingRequest()));
    }
}
