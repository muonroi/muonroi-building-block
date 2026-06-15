using Muonroi.Mediator.Mediator.Interfaces;

namespace Quickstart.Mediator.Api.Notifications;

/// <summary>
/// Notification published after an order is successfully created.
/// Implements <see cref="INotification"/> so MMediator fans it out to ALL
/// registered <see cref="INotificationHandler{TNotification}"/> implementations
/// — in this sample: <see cref="AuditOrderCreatedHandler"/> and <see cref="EmailOrderCreatedHandler"/>.
/// </summary>
public sealed class OrderCreatedNotification(Guid orderId, string productName, DateTimeOffset createdAt) : INotification
{
    public Guid OrderId { get; } = orderId;
    public string ProductName { get; } = productName;
    public DateTimeOffset CreatedAt { get; } = createdAt;
}

/// <summary>
/// Audit handler — writes an audit log entry for every new order.
/// </summary>
public sealed class AuditOrderCreatedHandler(ILogger<AuditOrderCreatedHandler> logger)
    : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[AUDIT] Order {OrderId} for product '{ProductName}' was created at {CreatedAt}.",
            notification.OrderId,
            notification.ProductName,
            notification.CreatedAt);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Email handler — simulates sending a confirmation e-mail for every new order.
/// Both handlers run sequentially (MNotificationStrategy.Sequential default).
/// </summary>
public sealed class EmailOrderCreatedHandler(ILogger<EmailOrderCreatedHandler> logger)
    : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[EMAIL] Sending confirmation email for order {OrderId} (product: '{ProductName}').",
            notification.OrderId,
            notification.ProductName);

        return Task.CompletedTask;
    }
}
