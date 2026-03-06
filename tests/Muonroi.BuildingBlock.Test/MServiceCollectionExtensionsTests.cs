namespace Muonroi.BuildingBlock.Test;

public class MServiceCollectionExtensionsTests
{
    public class PingRequest : IRequest<string>
    {
    }

    public class PingHandler : IRequestHandler<PingRequest, string>
    {
        public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("pong");
        }
    }

    [Fact]
    public async Task AddMediator_Registers_Mediator_Successfully()
    {
        ServiceCollection services = [];
        services.AddMediator([]);
        services.AddTransient<IRequestHandler<PingRequest, string>, PingHandler>();
        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.Send(new PingRequest());

        Assert.Equal("pong", result);
    }

    [Fact]
    public async Task AddMediator_Duplicate_Calls_Work_Idempotent()
    {
        ServiceCollection services = [];
        services.AddMediator([]);
        services.AddMediator([]);
        services.AddTransient<IRequestHandler<PingRequest, string>, PingHandler>();
        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.Send(new PingRequest());

        Assert.Equal("pong", result);
    }
}
