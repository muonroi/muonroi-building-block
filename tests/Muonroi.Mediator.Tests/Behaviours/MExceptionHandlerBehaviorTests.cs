using Microsoft.Extensions.DependencyInjection;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Interfaces;
namespace Muonroi.Mediator.Tests.Behaviours;

public class MExceptionHandlerBehaviorTests
{
    private record FaultyRequest(bool ShouldFail) : IRequest<string>;

    private class FaultyHandler : IRequestHandler<FaultyRequest, string>
    {
        public Task<string> Handle(FaultyRequest request, CancellationToken ct)
        {
            if (request.ShouldFail) throw new InvalidOperationException("handler failure");
            return Task.FromResult("ok");
        }
    }

    private class RecoverHandler : IRequestExceptionHandler<FaultyRequest, string, Exception>
    {
        public static bool WasCalled { get; set; }

        public Task HandleAsync(FaultyRequest request, Exception exception,
            RequestExceptionHandlerState<string> state, CancellationToken ct)
        {
            WasCalled = true;
            state.SetHandled("recovered");
            return Task.CompletedTask;
        }
    }

    private class PassThroughHandler : IRequestExceptionHandler<FaultyRequest, string, Exception>
    {
        public Task HandleAsync(FaultyRequest request, Exception exception,
            RequestExceptionHandlerState<string> state, CancellationToken ct)
        {
            // does not call SetHandled — passes through
            return Task.CompletedTask;
        }
    }

    private static IMediator BuildMediator(bool registerRecover, bool registerPassThrough = false)
    {
        RecoverHandler.WasCalled = false;
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<FaultyRequest, string>, FaultyHandler>();

        if (registerPassThrough)
            services.AddTransient<IRequestExceptionHandler<FaultyRequest, string, Exception>, PassThroughHandler>();
        if (registerRecover)
            services.AddTransient<IRequestExceptionHandler<FaultyRequest, string, Exception>, RecoverHandler>();

        services.AddTransient<IPipelineBehavior<FaultyRequest, string>, MExceptionHandlerBehavior<FaultyRequest, string>>();
        services.AddSingleton<IMediator>(sp => new MMediator(sp.GetService));
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task NoException_HandlerNotCalled()
    {
        IMediator mediator = BuildMediator(registerRecover: true);
        string result = await mediator.Send(new FaultyRequest(false));
        Assert.Equal("ok", result);
        Assert.False(RecoverHandler.WasCalled);
    }

    [Fact]
    public async Task ExceptionHandled_ReturnsRecoveredResponse()
    {
        IMediator mediator = BuildMediator(registerRecover: true);
        string result = await mediator.Send(new FaultyRequest(true));
        Assert.Equal("recovered", result);
        Assert.True(RecoverHandler.WasCalled);
    }

    [Fact]
    public async Task NoHandlerRegistered_ExceptionRethrown()
    {
        IMediator mediator = BuildMediator(registerRecover: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new FaultyRequest(true)));
    }

    [Fact]
    public async Task PassThroughThenRecover_FirstPassesSecondHandles()
    {
        IMediator mediator = BuildMediator(registerRecover: true, registerPassThrough: true);
        string result = await mediator.Send(new FaultyRequest(true));
        Assert.Equal("recovered", result);
        Assert.True(RecoverHandler.WasCalled);
    }
}
