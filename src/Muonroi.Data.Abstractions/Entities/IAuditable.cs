namespace Muonroi.Data.Abstractions.Entities;

/// <summary>
/// Indicates that an entity tracks creation and modification timestamps.
/// </summary>
public interface IAuditable
{
    /// <summary>Gets or sets the UTC timestamp when the entity was created.</summary>
    DateTime? CreatedDate { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the entity was last updated.</summary>
    DateTime? UpdatedDate { get; set; }
}

/// <summary>
/// Extends <see cref="IAuditable"/> with typed user tracking for created/updated actions.
/// </summary>
/// <typeparam name="TUserKey">The type of the user identifier (e.g., Guid, long, string).</typeparam>
public interface IAuditable<TUserKey> : IAuditable
{
    /// <summary>Gets or sets the identifier of the user who created the entity.</summary>
    TUserKey? CreatedBy { get; set; }

    /// <summary>Gets or sets the identifier of the user who last updated the entity.</summary>
    TUserKey? UpdatedBy { get; set; }
}
