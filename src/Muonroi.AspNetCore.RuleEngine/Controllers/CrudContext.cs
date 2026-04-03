namespace Muonroi.AspNetCore.Controllers;

/// <summary>
/// Represents the type of CRUD operation being performed.
/// </summary>
public enum CrudOperationType
{
    /// <summary>Creates a new entity.</summary>
    Create,
    /// <summary>Updates an existing entity.</summary>
    Update,
    /// <summary>Deletes an entity.</summary>
    Delete,
    /// <summary>Reads entity data.</summary>
    Read
}

/// <summary>
/// Context object passed to business rules during Auto CRUD operations.
/// Contains entity data, operation metadata, and validation state.
/// </summary>
/// <typeparam name="TEntity">The entity type being operated on.</typeparam>
public sealed class CrudContext<TEntity> where TEntity : MEntity
{
    /// <summary>
    /// The entity being created, updated, or deleted.
    /// </summary>
    public TEntity Entity { get; set; } = default!;

    /// <summary>
    /// The original entity state (for Update operations).
    /// Null for Create and Delete operations.
    /// </summary>
    public TEntity? OriginalEntity { get; set; }

    /// <summary>
    /// The type of CRUD operation being performed.
    /// </summary>
    public CrudOperationType OperationType { get; set; }

    /// <summary>
    /// The current user's ID performing the operation.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// The current tenant ID (for multi-tenant scenarios).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Validation errors collected during rule execution.
    /// </summary>
    public List<string> ValidationErrors { get; } = [];

    /// <summary>
    /// Additional metadata that can be set by rules.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; } = [];

    /// <summary>
    /// Indicates whether the operation should be cancelled.
    /// Rules can set this to true to prevent the operation from completing.
    /// </summary>
    public bool CancelOperation { get; set; }

    /// <summary>
    /// The reason for cancelling the operation (if CancelOperation is true).
    /// </summary>
    public string? CancellationReason { get; set; }
}
