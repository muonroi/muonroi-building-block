namespace Muonroi.BuildingBlock.Test;

public class MControllerBaseTests
{
    private class DummyController(IMediator mediator, ILogger logger, IMapper mapper)
        : MControllerBase(mediator, logger, mapper)
    {
        public IMediator MediatorProp => Mediator;
        public ILogger LoggerProp => Logger;
        public IMapper MapperProp => Mapper;
    }

    [Fact]
    public void Constructor_Initializes_Dependencies()
    {
        IMediator med = Substitute.For<IMediator>();
        ILogger log = Substitute.For<ILogger>();
        IMapper map = Substitute.For<IMapper>();

        DummyController ctrl = new(med, log, map);

        Assert.Same(med, ctrl.MediatorProp);
        Assert.Same(log, ctrl.LoggerProp);
        Assert.Same(map, ctrl.MapperProp);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        DummyController ctrl = new(null!, null!, null!);

        Assert.Null(ctrl.MediatorProp);
        Assert.Null(ctrl.LoggerProp);
        Assert.Null(ctrl.MapperProp);
    }

    [Fact]
    public void Mediator_Returns_Null_When_Not_Injected()
    {
        DummyController ctrl = new(null!, null!, null!);
        Assert.Null(ctrl.MediatorProp);
    }

    [Fact]
    public void Mediator_Returns_Value()
    {
        IMediator med = Substitute.For<IMediator>();
        DummyController ctrl = new(med, null!, null!);
        Assert.Same(med, ctrl.MediatorProp);
    }

    [Fact]
    public void Logger_Returns_Null_When_Not_Injected()
    {
        DummyController ctrl = new(null!, null!, null!);
        Assert.Null(ctrl.LoggerProp);
    }

    [Fact]
    public void Logger_Returns_Value()
    {
        ILogger log = Substitute.For<ILogger>();
        DummyController ctrl = new(null!, log, null!);
        Assert.Same(log, ctrl.LoggerProp);
    }

    [Fact]
    public void Mapper_Returns_Null_When_Not_Injected()
    {
        DummyController ctrl = new(null!, null!, null!);
        Assert.Null(ctrl.MapperProp);
    }

    [Fact]
    public void Mapper_Returns_Value()
    {
        IMapper map = Substitute.For<IMapper>();
        DummyController ctrl = new(null!, null!, map);
        Assert.Same(map, ctrl.MapperProp);
    }
}
