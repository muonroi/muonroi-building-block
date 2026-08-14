namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Provides access to dynamic routing table entries.
/// </summary>
public interface IDynamicRoutingTableStore
{
    /// <summary>
    /// Gets active routing entries for the supplied message type and tenant.
    /// </summary>
    /// <param name="messageType">The fully qualified message type name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The ordered active routing entries.</returns>
    Task<IReadOnlyList<RoutingTableEntry>> GetRoutesAsync(
        string messageType,
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a routing table entry.
    /// </summary>
    /// <param name="entry">The routing entry to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpsertRouteAsync(RoutingTableEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Removes a routing table entry.
    /// </summary>
    /// <param name="messageType">The fully qualified message type name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ruleCode">The routing rule code.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveRouteAsync(string messageType, string tenantId, string ruleCode, CancellationToken ct = default);
}

/// <summary>
/// Represents a persisted routing table entry.
/// </summary>
/// <param name="MessageType">The fully qualified message type name.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="RuleCode">The unique routing rule code.</param>
/// <param name="Order">The execution order for the route.</param>
/// <param name="TargetAddress">The destination transport address.</param>
/// <param name="FeelExpression">Optional FEEL predicate evaluated against message properties.</param>
/// <param name="IsActive">Indicates whether the route is active.</param>
public sealed record RoutingTableEntry(
    string MessageType,
    string TenantId,
    string RuleCode,
    int Order,
    string TargetAddress,
    string? FeelExpression,
    bool IsActive = true);
