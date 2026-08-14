namespace Muonroi.Mediator.Tests.Behaviours;

public class MPrePostProcessorTests
{
    private record DataRequest(string Input) : IRequest<string>;

    private class DataHandler : IRequestHandler<DataRequest, string>
    {
        public Task<string> Handle(DataRequest request, CancellationToken ct)
            => Task.FromResult($"result:{request.Input}");
    }

    private class PreProcessor1 : IRequestPreProcessor<DataRequest>
    {
        public static List<string> Log { get; } = [];
        public Task ProcessAsync(DataRequest request, CancellationToken ct)
        {
            Log.Add($"pre1:{request.Input}");
            return Task.CompletedTask;
        }
    }

    private class PreProcessor2 : IRequestPreProcessor<DataRequest>
    {
        public static List<string> Log { get; } = [];
        public Task ProcessAsync(DataRequest request, CancellationToken ct)
        {
            Log.Add($"pre2:{request.Input}");
            return Task.CompletedTask;
        }
    }

    private class PostProcessor1 : IRequestPostProcessor<DataRequest, string>
    {
        public static List<string> Log { get; } = [];
        public Task ProcessAsync(DataRequest request, string response, CancellationToken ct)
        {
            Log.Add($"post1:{response}");
            return Task.CompletedTask;
        }
    }

    private static IMediator BuildMediator()
    {
        PreProcessor1.Log.Clear();
        PreProcessor2.Log.Clear();
        PostProcessor1.Log.Clear();

        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<DataRequest, string>, DataHandler>();
        services.AddTransient<IRequestPreProcessor<DataRequest>, PreProcessor1>();
        services.AddTransient<IRequestPreProcessor<DataRequest>, PreProcessor2>();
        services.AddTransient<IRequestPostProcessor<DataRequest, string>, PostProcessor1>();
        services.AddTransient<IPipelineBehavior<DataRequest, string>, MPreProcessorBehavior<DataRequest, string>>();
        services.AddTransient<IPipelineBehavior<DataRequest, string>, MPostProcessorBehavior<DataRequest, string>>();
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task PreProcessors_BothCalledBeforeHandler()
    {
        IMediator mediator = BuildMediator();
        await mediator.Send(new DataRequest("abc"));

        Assert.Contains("pre1:abc", PreProcessor1.Log);
        Assert.Contains("pre2:abc", PreProcessor2.Log);
    }

    [Fact]
    public async Task PostProcessor_CalledWithHandlerResponse()
    {
        IMediator mediator = BuildMediator();
        string result = await mediator.Send(new DataRequest("xyz"));
        Assert.Equal("result:xyz", result);
        Assert.Contains("post1:result:xyz", PostProcessor1.Log);
    }

    [Fact]
    public async Task NoProcessors_HandlerStillExecutes()
    {
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<DataRequest, string>, DataHandler>();
        services.AddTransient<IPipelineBehavior<DataRequest, string>, MPreProcessorBehavior<DataRequest, string>>();
        services.AddTransient<IPipelineBehavior<DataRequest, string>, MPostProcessorBehavior<DataRequest, string>>();
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));

        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        string result = await mediator.Send(new DataRequest("test"));
        Assert.Equal("result:test", result);
    }
}
