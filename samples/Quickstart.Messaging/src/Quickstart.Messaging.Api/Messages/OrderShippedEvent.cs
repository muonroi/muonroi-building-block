namespace Quickstart.Messaging.Api.Messages;

/// <summary>
/// Published when an order has been shipped.
/// </summary>
/// <param name="OrderId">Unique identifier for the order.</param>
/// <param name="TrackingNumber">Carrier tracking number assigned to the shipment.</param>
/// <param name="ShippedAt">UTC timestamp when the order left the warehouse.</param>
public record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    DateTimeOffset ShippedAt);
