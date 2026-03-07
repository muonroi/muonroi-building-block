namespace Muonroi.AspNetCore.Tests;

public class MControllerBaseTests
{
    private sealed class DummyController(IMediator mediator, ILogger logger)
        : MControllerBase(mediator, logger)
    {
        public IMediator MediatorProp => Mediator;
        public ILogger LoggerProp => Logger;
    }

    [Fact]
    public void Constructor_Initializes_Dependencies()
    {
        IMediator mediator = Substitute.For<IMediator>();
        ILogger logger = Substitute.For<ILogger>();

        DummyController controller = new(mediator, logger);

        Assert.Same(mediator, controller.MediatorProp);
        Assert.Same(logger, controller.LoggerProp);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        DummyController controller = new(null!, null!);

        Assert.Null(controller.MediatorProp);
        Assert.Null(controller.LoggerProp);
    }

    [Fact]
    public void Mediator_Returns_Value()
    {
        IMediator mediator = Substitute.For<IMediator>();
        DummyController controller = new(mediator, null!);
        Assert.Same(mediator, controller.MediatorProp);
    }

    [Fact]
    public void Logger_Returns_Value()
    {
        ILogger logger = Substitute.For<ILogger>();
        DummyController controller = new(null!, logger);
        Assert.Same(logger, controller.LoggerProp);
    }
}
