namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found.
/// </summary>
public sealed class MNotFoundException : MException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MNotFoundException"/> class.
    /// </summary>
    /// <param name="entityName">The name of the entity being requested.</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    public MNotFoundException(string entityName, object entityId)
        : base("NOT_FOUND", $"{entityName} with id '{entityId}' was not found.", MExceptionCategory.Domain, 404)
    {
        Details["EntityName"] = entityName;
        Details["EntityId"] = entityId;
    }
}
