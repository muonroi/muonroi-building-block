using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.Messaging.Abstractions.Events;

public class MEntityCreatedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    public T Data { get; set; } = entity;
}

public class MEntityChangedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    public T Data { get; set; } = entity;
}

public class MEntityDeletedEvent<T>(T entity) : IMDomainEvent where T : MEntity
{
    public T Data { get; set; } = entity;
}

public class MEntitiesCreatedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    public IEnumerable<T> Data { get; set; } = entities;
}

public class MEntitiesChangedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    public IEnumerable<T> Data { get; set; } = entities;
}

public class MEntitiesDeletedEvent<T>(IEnumerable<T> entities) : IMDomainEvent where T : MEntity
{
    public IEnumerable<T> Data { get; set; } = entities;
}
