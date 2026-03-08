namespace Muonroi.Core.Abstractions.SeedWorks;

/// <summary>
/// Defines a marker interface for notifications.
/// </summary>
public interface INotification;

/// <summary>
/// Defines a marker interface for domain events.
/// </summary>
public interface IMDomainEvent : INotification;
