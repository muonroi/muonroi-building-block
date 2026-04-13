using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Mapper.Mapper;

namespace Muonroi.Mapper.Tests;

public class MapperServiceCollectionExtensionsTests
{
    [Fact]
    public void ConfigureMapper_WithExplicitAssembly_ShouldRegisterMapperAndMappings()
    {
        ServiceCollection services = new();

        services.ConfigureMapper(typeof(MappedDestination).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();

        IMapper mapper = provider.GetRequiredService<IMapper>();
        MappedDestination result = mapper.Map<MappedDestination>(new MappingSource { Name = "mapped" });

        Assert.Equal("mapped", result.Name);
    }

    [Fact]
    public void ConfigureMapper_WithoutAssemblies_ShouldStillRegisterServices()
    {
        ServiceCollection services = new();

        services.ConfigureMapper();
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMapper>());
        Assert.NotNull(provider.GetService<MappingConfiguration>());
    }

    public sealed class MappingSource
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class MappedDestination : IMapFrom<MappingSource>
    {
        public string Name { get; set; } = string.Empty;
    }
}
