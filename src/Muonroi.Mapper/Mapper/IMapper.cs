namespace Muonroi.Mapper.Mapper;

public interface IMapper
{
    TDestination Map<TDestination>(object source);
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    object Map(object source, object destination);
}
