namespace Muonroi.Mapper.Tests;

using Muonroi.Mapper.Mapper;
using Xunit;

public class MapperTests
{
    public class Source
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class Destination
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [Fact]
    public void SimpleMapper_Map_ShouldCopyMatchingProperties()
    {
        // Arrange
        var config = new MappingConfiguration();
        var mapper = new SimpleMapper(config);
        var source = new Source { Name = "John", Age = 30 };

        // Act
        var result = mapper.Map<Destination>(source);

        // Assert
        Assert.Equal(source.Name, result.Name);
        Assert.Equal(source.Age, result.Age);
    }
}
