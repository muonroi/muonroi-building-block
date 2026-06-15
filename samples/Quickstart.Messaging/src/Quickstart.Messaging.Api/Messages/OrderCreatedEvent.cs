namespace Quickstart.Messaging.Api.Messages;

/// <summary>
/// Published when a new order is placed.
/// </summary>
/// <param name="OrderId">Unique identifier for the order.</param>
/// <param name="Product">Name or SKU of the ordered product.</param>
/// <param name="Total">Total amount for the order.</param>
/// <param name="TenantId">Identifier of the tenant that owns the order.</param>
public record OrderCreatedEvent(
    Guid OrderId,
    string Product,
    decimal Total,
    string TenantId);
