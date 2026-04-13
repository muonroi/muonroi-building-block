using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MapperTests
{
    public class Source
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    public class Dest : IMapFrom<Source>
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public void ConfigureMapper_Maps_Properties()
    {
        ServiceCollection services = [];
        services.ConfigureMapper(typeof(MapperTests).Assembly);
        ServiceProvider sp = services.BuildServiceProvider();
        IMapper mapper = sp.GetRequiredService<IMapper>();

        Source src = new()
        {
            Name = "abc",
            Value = 5
        };
        Dest dest = mapper.Map<Dest>(src);

        Assert.Equal(src.Name, dest.Name);
        Assert.Equal(src.Value, dest.Value);
    }

    [Fact]
    public void Map_NullSource_Throws()
    {
        ServiceCollection services = [];
        services.ConfigureMapper(typeof(MapperTests).Assembly);
        ServiceProvider sp = services.BuildServiceProvider();
        IMapper mapper = sp.GetRequiredService<IMapper>();

        Assert.Throws<MArgumentException>(() => mapper.Map<Dest>(null!));
    }

    [Fact]
    public void Map_ToExistingDestination()
    {
        ServiceCollection services = [];
        services.ConfigureMapper(typeof(MapperTests).Assembly);
        ServiceProvider sp = services.BuildServiceProvider();
        IMapper mapper = sp.GetRequiredService<IMapper>();

        Source src = new()
        {
            Name = "x",
            Value = 1
        };
        Dest dest = new();
        mapper.Map(src, dest);

        Assert.Equal(src.Name, dest.Name);
        Assert.Equal(src.Value, dest.Value);
    }
}
