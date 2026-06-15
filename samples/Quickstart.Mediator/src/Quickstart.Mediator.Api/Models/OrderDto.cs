namespace Quickstart.Mediator.Api.Models;

/// <summary>
/// Data transfer object representing an order.
/// </summary>
public record OrderDto(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Status,
    DateTimeOffset CreatedAt);
