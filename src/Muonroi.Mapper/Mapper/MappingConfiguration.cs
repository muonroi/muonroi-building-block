namespace Muonroi.Mapper.Mapper;

/// <summary>
/// Stores mapping actions between source and destination types.
/// </summary>
public sealed class MappingConfiguration
{
    private readonly ConcurrentDictionary<(Type Source, Type Destination), Action<object, object>> _maps = new();

    internal Action<object, object> GetOrAdd(Type sourceType, Type destinationType)
    {
        return _maps.GetOrAdd((sourceType, destinationType),
            static key => CreateMapAction(key.Source, key.Destination));
    }

    internal static Action<object, object> CreateMapAction(Type sourceType, Type destinationType)
    {
        ParameterExpression srcObj = Expression.Parameter(typeof(object), "src");
        ParameterExpression destObj = Expression.Parameter(typeof(object), "dest");

        UnaryExpression source = Expression.Convert(srcObj, sourceType);
        UnaryExpression dest = Expression.Convert(destObj, destinationType);

        List<BinaryExpression> assigns = [];

        foreach (PropertyInfo? destProp in destinationType.GetProperties().Where(p => p.CanWrite))        
        {
            PropertyInfo? sourceProp = sourceType.GetProperty(destProp.Name);
            if (sourceProp == null || !sourceProp.CanRead) continue;
            if (!destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType)) continue;
            assigns.Add(Expression.Assign(Expression.Property(dest, destProp),
                Expression.Property(source, sourceProp)));
        }

        Expression body = Expression.Block(assigns);
        return Expression.Lambda<Action<object, object>>(body, srcObj, destObj).Compile();
    }
}
