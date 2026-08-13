namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Represents the points in rule execution where hooks can be invoked.
/// </summary>
public enum HookPoint
{
    /// <summary>
    /// Executes before a rule is fired.
    /// </summary>
    BeforeRule,

    /// <summary>
    /// Executes after a rule is fired.
    /// </summary>
    AfterRule,

    /// <summary>
    /// Executes when an error occurs during rule execution.
    /// </summary>
    Error,
    /// <summary>
    /// Executes before input validation happens.
    /// </summary>
    BeforeValidateInput,

    /// <summary>
    /// Executes before mapping from request to domain objects.
    /// </summary>
    BeforeMap,

    /// <summary>
    /// Executes before persisting data.
    /// </summary>
    BeforePersist,

    /// <summary>
    /// Executes after persisting data.
    /// </summary>
    AfterPersist,

    /// <summary>
    /// Executes when the operation succeeds.
    /// </summary>
    OnSuccess,

    /// <summary>
    /// Executes when the operation fails.
    /// </summary>
    OnFailure,

    /// <summary>
    /// Executes before creating a new entity in Auto CRUD operations.
    /// </summary>
    BeforeCreate,

    /// <summary>
    /// Executes after creating a new entity in Auto CRUD operations.
    /// </summary>
    AfterCreate,

    /// <summary>
    /// Executes before updating an existing entity in Auto CRUD operations.
    /// </summary>
    BeforeUpdate,

    /// <summary>
    /// Executes after updating an existing entity in Auto CRUD operations.
    /// </summary>
    AfterUpdate,

    /// <summary>
    /// Executes before deleting an entity in Auto CRUD operations.
    /// </summary>
    BeforeDelete,

    /// <summary>
    /// Executes after deleting an entity in Auto CRUD operations.
    /// </summary>
    AfterDelete
}
