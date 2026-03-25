namespace Muonroi.Mapping.Abstractions;

/// <summary>
/// Defines the mapping contract between an entity and its DTO representation.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type.</typeparam>
public interface IEntityMapper<TEntity, TDto>
    where TEntity : class
    where TDto : class
{
    /// <summary>Maps an entity to its DTO representation.</summary>
    TDto ToDto(TEntity entity);

    /// <summary>Maps a DTO to a new entity instance.</summary>
    TEntity ToEntity(TDto dto);

    /// <summary>Applies DTO values onto an existing entity instance (for update scenarios).</summary>
    void ApplyUpdate(TEntity entity, TDto dto);
}
