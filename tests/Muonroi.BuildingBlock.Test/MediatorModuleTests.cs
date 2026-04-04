using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MediatorModuleTests
{
    private class PingRequest : IRequest<string>
    {
    }

    private class PingHandler : IRequestHandler<PingRequest, string>
    {
        public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("pong");
        }
    }

    [Fact]
    public async Task Load_Registers_Mediator_And_Handlers_Successfully()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new MediatorModule());
        builder.RegisterType<PingHandler>().As<IRequestHandler<PingRequest, string>>();
        builder.RegisterInstance(new LoggerConfiguration().CreateLogger()).As<ILogger>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        IMediator mediator = scope.Resolve<IMediator>();
        IPipelineBehavior<PingRequest, string> behavior = scope.Resolve<IPipelineBehavior<PingRequest, string>>();

        string result = await mediator.Send(new PingRequest());

        Assert.NotNull(mediator);
        Assert.NotNull(behavior);
        Assert.Equal("pong", result);
    }

    [Fact]
    public void Load_Throws_When_Missing_Logger()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new MediatorModule());
        builder.RegisterType<PingHandler>().As<IRequestHandler<PingRequest, string>>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        Assert.Throws<DependencyResolutionException>(() => scope.Resolve<IPipelineBehavior<PingRequest, string>>());
    }

    [Fact]
    public async Task Load_Throws_When_Handler_Not_Registered()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new MediatorModule());
        builder.RegisterInstance(new LoggerConfiguration().CreateLogger()).As<ILogger>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        IMediator mediator = scope.Resolve<IMediator>();
        await Assert.ThrowsAsync<MInternalException>(() => mediator.Send(new PingRequest()));
    }

    [Fact]
    public void Load_Multiple_Module_Registrations_Work()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new MediatorModule());
        builder.RegisterModule(new MediatorModule());
        builder.RegisterType<PingHandler>().As<IRequestHandler<PingRequest, string>>();
        builder.RegisterInstance(new LoggerConfiguration().CreateLogger()).As<ILogger>();
        IContainer container = builder.Build();

        int regs = container.ComponentRegistry.RegistrationsFor(new TypedService(typeof(IMediator))).Count();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        IMediator mediator = scope.Resolve<IMediator>();

        Assert.NotNull(mediator);
        Assert.Equal(2, regs);
    }
}
