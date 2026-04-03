using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Mapper.Mapper;

/// <summary>
/// Reflection-based mapper using cached mapping actions.
/// </summary>
public sealed class SimpleMapper(MappingConfiguration configuration) : IMapper
{
    /// <inheritdoc/>
    public TDestination Map<TDestination>(object source)
    {
        MGuard.NotNull(source);

        TDestination destination = Activator.CreateInstance<TDestination>()!;
        Action<object, object> action = configuration.GetOrAdd(source.GetType(), typeof(TDestination));
        action(source, destination);
        return destination;
    }

    /// <inheritdoc/>
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Action<object, object> action = configuration.GetOrAdd(typeof(TSource), typeof(TDestination));
        action(source, destination);
        return destination;
    }

    /// <inheritdoc/>
    public object Map(object source, object destination)
    {
        MGuard.NotNull(source);
        MGuard.NotNull(destination);

        Action<object, object> action = configuration.GetOrAdd(source.GetType(), destination.GetType());
        action(source, destination);
        return destination;
    }
}
