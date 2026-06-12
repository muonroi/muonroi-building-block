using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Mediator.Tests.Pipeline;

/// <summary>
/// Verifies the compiled-delegate pipeline via the public MMediator API.
/// </summary>
public class CompiledDelegateTests
{
    private record EchoRequest(string Input) : IRequest<string>;
    private record BoxedRequest(int Value) : IRequest<string>;

    private class EchoHandler : IRequestHandler<EchoRequest, string>
    {
        public Task<string> Handle(EchoRequest request, CancellationToken ct)
            => Task.FromResult(request.Input.ToUpperInvariant());
    }

    private class BoxedHandler : IRequestHandler<BoxedRequest, string>
    {
        public Task<string> Handle(BoxedRequest request, CancellationToken ct)
            => Task.FromResult(request.Value.ToString());
    }

    // Behavior is registered manually (factory delegate) — not via open-generic DI scan.
    // TPhantom is a phantom type parameter that makes arity (3) != IPipelineBehavior<,> arity (2),
    // so the assembly scan's arity guard skips this type and DI never tries to auto-resolve it.
    private class TagBehavior<TReq, TRes, TPhantom>(List<string> log, string tag) : IPipelineBehavior<TReq, TRes>
        where TReq : IRequest<TRes>
    {
        private readonly List<string> _log = log;
        private readonly string _tag = tag;

        public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
        {
            _log.Add($"{_tag}:before");
            TRes r = await next();
            _log.Add($"{_tag}:after");
            return r;
        }
    }

    [Fact]
    public async Task Send_ReturnsCorrectResult()
    {
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<EchoRequest, string>, EchoHandler>();
        services.AddMediator(typeof(CompiledDelegateTests).Assembly);
        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        string result = await mediator.Send(new EchoRequest("hello"));
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public async Task Send_DifferentRequestTypes_BothHandled()
    {
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<EchoRequest, string>, EchoHandler>();
        services.AddTransient<IRequestHandler<BoxedRequest, string>, BoxedHandler>();
        services.AddMediator(typeof(CompiledDelegateTests).Assembly);
        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        string r1 = await mediator.Send(new EchoRequest("hello"));
        string r2 = await mediator.Send(new BoxedRequest(42));

        Assert.Equal("HELLO", r1);
        Assert.Equal("42", r2);
    }

    [Fact]
    public async Task Pipeline_BehaviorsExecutedInCorrectOrder()
    {
        List<string> log = [];
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<EchoRequest, string>, EchoHandler>();
        services.AddTransient<IPipelineBehavior<EchoRequest, string>>(_ => new TagBehavior<EchoRequest, string, object>(log, "B1"));
        services.AddTransient<IPipelineBehavior<EchoRequest, string>>(_ => new TagBehavior<EchoRequest, string, object>(log, "B2"));
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));

        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        await mediator.Send(new EchoRequest("x"));

        // B1 registered first → outermost after reverse
        Assert.Equal(["B1:before", "B2:before", "B2:after", "B1:after"], log);
    }

    [Fact]
    public async Task Send_ThrowsWhenHandlerNotRegistered()
    {
        ServiceCollection services = new();
        // No handler for EchoRequest registered
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));
        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<MInternalException>(
            () => mediator.Send(new EchoRequest("test")));
    }

    [Fact]
    public async Task Send_CalledTwice_BothSucceed()
    {
        // Verifies that the cached wrapper works correctly on repeat calls
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<EchoRequest, string>, EchoHandler>();
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));
        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        string r1 = await mediator.Send(new EchoRequest("a"));
        string r2 = await mediator.Send(new EchoRequest("b"));

        Assert.Equal("A", r1);
        Assert.Equal("B", r2);
    }
}
