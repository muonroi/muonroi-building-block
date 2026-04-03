namespace Muonroi.Mapping.Abstractions;

/// <summary>
/// Abstract base class implementing the template method pattern for entity-DTO mapping.
/// Core fields are mapped via abstract methods; site-specific fields are mapped via virtual no-op overrides.
/// Consumers override <c>MapSiteSpecificToDto</c> / <c>MapSiteSpecificToEntity</c> to add schema-divergent fields.
/// </summary>
/// <typeparam name="TEntity">The entity type, must have a parameterless constructor.</typeparam>
/// <typeparam name="TDto">The DTO type, must have a parameterless constructor.</typeparam>
public abstract class EntityMapperBase<TEntity, TDto> : IEntityMapper<TEntity, TDto>
    where TEntity : class, new()
    where TDto : class, new()
{
    /// <inheritdoc/>
    public TDto ToDto(TEntity entity)
    {
        var dto = new TDto();
        MapCoreToDto(entity, dto);
        MapSiteSpecificToDto(entity, dto);
        return dto;
    }

    /// <inheritdoc/>
    public TEntity ToEntity(TDto dto)
    {
        var entity = new TEntity();
        MapCoreToEntity(dto, entity);
        MapSiteSpecificToEntity(dto, entity);
        return entity;
    }

    /// <inheritdoc/>
    public void ApplyUpdate(TEntity entity, TDto dto)
    {
        MapCoreToEntity(dto, entity);
        MapSiteSpecificToEntity(dto, entity);
    }

    /// <summary>
    /// Maps core (shared) fields from entity to DTO. Must be implemented by all mappers.
    /// </summary>
    protected abstract void MapCoreToDto(TEntity entity, TDto dto);

    /// <summary>
    /// Maps core (shared) fields from DTO to entity. Must be implemented by all mappers.
    /// </summary>
    protected abstract void MapCoreToEntity(TDto dto, TEntity entity);

    /// <summary>
    /// Maps site-specific fields from entity to DTO. Override to add schema-divergent fields.
    /// Default implementation is a no-op.
    /// </summary>
    protected virtual void MapSiteSpecificToDto(TEntity entity, TDto dto) { }

    /// <summary>
    /// Maps site-specific fields from DTO to entity. Override to add schema-divergent fields.
    /// Default implementation is a no-op.
    /// </summary>
    protected virtual void MapSiteSpecificToEntity(TDto dto, TEntity entity) { }
}
