namespace Muonroi.AspNetCore.DI.Autofac;

internal class MediatorModule : global::Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register the mediator implementation
        builder.RegisterType<MMediator>().As<IMediator>();

        // Pipeline behaviors registered in order
        builder.RegisterGeneric(typeof(ValidationBehavior<,>)).As(typeof(IPipelineBehavior<,>));
        builder.RegisterGeneric(typeof(LoggingBehavior<,>)).As(typeof(IPipelineBehavior<,>));
    }
}
