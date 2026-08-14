namespace Muonroi.Mediator.Tests;

public class ServiceCollectionExtensionsTests
{
    private record TestRequest : IRequest<string>;

    [Fact]
    public void AddMMediator_RegistersMediator()
    {
        ServiceCollection services = [];
        services.AddMMediator();

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        mediator.Should().NotBeNull();
        mediator.Should().BeOfType<MMediator>();
    }

    [Fact]
    public void AddMMediator_RegistersRequestContextBag()
    {
        ServiceCollection services = [];
        services.AddMMediator();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IRequestContextBag bag = scope.ServiceProvider.GetRequiredService<IRequestContextBag>();

        bag.Should().NotBeNull();
    }

    [Fact]
    public void AddMMediator_RequestContextBag_ShouldStoreValuesWithinScope()
    {
        ServiceCollection services = [];
        services.AddMMediator();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IRequestContextBag bag = scope.ServiceProvider.GetRequiredService<IRequestContextBag>();

        bag.Set("correlation", "corr-1");

        bag.Contains("correlation").Should().BeTrue();
        bag.Get<string>("correlation").Should().Be("corr-1");
        bag.Get<int>("missing").Should().Be(0);
    }

    [Fact]
    public void AddMediator_BackwardCompatible_RegistersMediator()
    {
        ServiceCollection services = [];
        services.AddMediator();

        ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        mediator.Should().BeOfType<MMediator>();
    }

    [Fact]
    public void MMediatorOptions_AddBehavior_DoesNotThrow()
    {
        MMediatorOptions options = new();

        // AddBehavior should not throw when adding behaviors
        Action act = () =>
        {
            options.AddBehavior(typeof(IPipelineBehavior<,>));
            options.AddBehavior(typeof(IPipelineBehavior<,>));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void MMediatorOptions_AddBehaviorGeneric_DoesNotThrow()
    {
        MMediatorOptions options = new();

        Action act = () => options.AddBehavior<IPipelineBehavior<TestRequest, string>>();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddMuonroiEcosystem_RegistersExpectedBehaviorOrder()
    {
        ServiceCollection services = [];
        services.AddMMediator(options => options.AddMuonroiEcosystem());

        services
            .Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .Should()
            .Equal(
            typeof(Muonroi.Mediator.Behaviours.MPostProcessorBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.MPreProcessorBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.ValidationBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.MAuthorizationBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.MTenantValidationBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.MDiagnosticsBehavior<,>),
            typeof(Muonroi.Mediator.Behaviours.MExceptionHandlerBehavior<,>));
    }
}
