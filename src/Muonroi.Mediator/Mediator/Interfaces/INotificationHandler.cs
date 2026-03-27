namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the INotification Handler{TNotification}.
/// </summary>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
