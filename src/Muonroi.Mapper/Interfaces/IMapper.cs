namespace Muonroi.Mapper.Interfaces;

/// <summary>
/// Provides object mapping utilities.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps a source object to a new destination instance.
    /// </summary>
    TDestination Map<TDestination>(object source);
    /// <summary>
    /// Maps a source object onto an existing destination instance.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    /// <summary>
    /// Maps a source object onto an existing destination object.
    /// </summary>
    object Map(object source, object destination);
}
