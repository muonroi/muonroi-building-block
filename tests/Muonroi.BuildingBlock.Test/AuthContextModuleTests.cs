namespace Muonroi.BuildingBlock.Test;

public class AuthContextModuleTests
{
    private class FakeAuthContextFactory : IAuthContextFactory
    {
        public int Calls { get; private set; }

        public MAuthenticateInfoContext Create()
        {
            Calls++;
            return new MAuthenticateInfoContext(false);
        }
    }

    [Fact]
    public void Load_Registers_Services_Successfully()
    {
        FakeAuthContextFactory factory = new();
        ContainerBuilder builder = new();
        builder.RegisterModule(new AuthContextModule());
        builder.RegisterInstance(factory).As<IAuthContextFactory>();
        builder.RegisterInstance(NullLogger<AuthenticateHeaderHandler>.Instance)
            .As<ILogger<AuthenticateHeaderHandler>>();
        builder.RegisterInstance(new ConfigurationBuilder().Build()).As<IConfiguration>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        IAmqpContext amqp = scope.Resolve<IAmqpContext>();
        MAuthenticateInfoContext authCtx = scope.Resolve<MAuthenticateInfoContext>();
        ICurrentUserContext currentCtx = scope.Resolve<ICurrentUserContext>();
        AuthenticateHeaderHandler handler = scope.Resolve<AuthenticateHeaderHandler>();

        Assert.NotNull(amqp);
        Assert.Same(authCtx, currentCtx);
        Assert.NotNull(handler);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public void Load_Throws_When_Missing_AuthFactory()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new AuthContextModule());
        builder.RegisterInstance(NullLogger<AuthenticateHeaderHandler>.Instance)
            .As<ILogger<AuthenticateHeaderHandler>>();
        builder.RegisterInstance(new ConfigurationBuilder().Build()).As<IConfiguration>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        Assert.Throws<DependencyResolutionException>(() => scope.Resolve<MAuthenticateInfoContext>());
    }

    [Fact]
    public void Load_Throws_When_Missing_Configuration()
    {
        ContainerBuilder builder = new();
        builder.RegisterModule(new AuthContextModule());
        builder.RegisterInstance(new FakeAuthContextFactory()).As<IAuthContextFactory>();
        builder.RegisterInstance(NullLogger<AuthenticateHeaderHandler>.Instance)
            .As<ILogger<AuthenticateHeaderHandler>>();
        IContainer container = builder.Build();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        Assert.Throws<DependencyResolutionException>(() => scope.Resolve<AuthenticateHeaderHandler>());
    }

    [Fact]
    public void Load_Multiple_Module_Registrations_Work()
    {
        FakeAuthContextFactory factory = new();
        ContainerBuilder builder = new();
        builder.RegisterInstance(factory).As<IAuthContextFactory>();
        builder.RegisterInstance(NullLogger<AuthenticateHeaderHandler>.Instance)
            .As<ILogger<AuthenticateHeaderHandler>>();
        builder.RegisterInstance(new ConfigurationBuilder().Build()).As<IConfiguration>();
        builder.RegisterModule(new AuthContextModule());
        builder.RegisterModule(new AuthContextModule());
        IContainer container = builder.Build();

        int regs = container.ComponentRegistry.RegistrationsFor(new TypedService(typeof(IAmqpContext))).Count();

        using ILifetimeScope scope = container.BeginLifetimeScope();
        IAmqpContext ctx = scope.Resolve<IAmqpContext>();

        Assert.NotNull(ctx);
        Assert.Equal(2, regs);
    }
}
