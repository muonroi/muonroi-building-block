using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Mapper.Mapper;

public static class MapperServiceCollectionExtensions
{
    public static IServiceCollection ConfigureMapper(this IServiceCollection services, params Assembly[]? assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        MappingConfiguration configuration = new();
        foreach (Assembly assembly in assemblies)
        {
            ApplyMappingsFromAssembly(configuration, assembly);
        }

        services.AddSingleton(configuration);
        services.AddSingleton<IMapper, SimpleMapper>();
        return services;
    }

    private static void ApplyMappingsFromAssembly(MappingConfiguration config, Assembly assembly)
    {
        Type mapFromType = typeof(IMapFrom<>);
        IEnumerable<Type> types = assembly.GetExportedTypes()
            .Where(t => Array.Exists(t.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == mapFromType));

        foreach (Type? type in types)
        {
            IEnumerable<Type> mapFromInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapFromType);
            foreach (Type? mapFromInterface in mapFromInterfaces)
            {
                Type sourceType = mapFromInterface.GetGenericArguments()[0];
                config.GetOrAdd(sourceType, type);
                config.GetOrAdd(type, sourceType);
            }
        }
    }
}
