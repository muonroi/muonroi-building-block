namespace Muonroi.Messaging.Abstractions.Events;

/// <summary>
/// Represents the MEntity Created Event T.
/// </summary>
public class MEntityCreatedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}

/// <summary>
/// Represents the MEntity Changed Event.
/// </summary>
public class MEntityChangedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}

/// <summary>
/// Represents the MEntity Deleted Event.
/// </summary>
public class MEntityDeletedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public T Data { get; set; } = entity;
}

/// <summary>
/// Represents the MEntities Created Event.
/// </summary>
public class MEntitiesCreatedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}

/// <summary>
/// Represents the MEntities Changed Event T.
/// </summary>
public class MEntitiesChangedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}

/// <summary>
/// Represents the MEntities Deleted Event.
/// </summary>
public class MEntitiesDeletedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = entities;
}
