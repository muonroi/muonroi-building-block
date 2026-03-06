using Muonroi.Mapper.Interfaces;

namespace Muonroi.Mapper.Mapper;

public sealed class SimpleMapper(MappingConfiguration configuration) : IMapper
{
    public TDestination Map<TDestination>(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        TDestination destination = Activator.CreateInstance<TDestination>()!;
        Action<object, object> action = configuration.GetOrAdd(source.GetType(), typeof(TDestination));   
        action(source, destination);
        return destination;
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (destination is null) throw new ArgumentNullException(nameof(destination));

        Action<object, object> action = configuration.GetOrAdd(typeof(TSource), typeof(TDestination));    
        action(source, destination);
        return destination;
    }

    public object Map(object source, object destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Action<object, object> action = configuration.GetOrAdd(source.GetType(), destination.GetType());  
        action(source, destination);
        return destination;
    }
}
