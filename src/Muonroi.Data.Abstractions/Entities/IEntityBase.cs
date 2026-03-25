namespace Muonroi.Data.Abstractions.Entities;

/// <summary>
/// Marker interface for all entities in the Muonroi ecosystem.
/// Consumer entities implement this interface directly without inheriting MEntity.
/// </summary>
public interface IEntityBase
{
    // Marker interface — consumer decides what properties exist
}

/// <summary>
/// Entity contract that requires a typed primary key.
/// </summary>
/// <typeparam name="TKey">The type of the primary key (e.g., long, Guid, int).</typeparam>
public interface IEntityBase<TKey> : IEntityBase
{
    /// <summary>Gets or sets the primary key of the entity.</summary>
    TKey Id { get; set; }
}
