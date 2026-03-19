namespace Muonroi.AuthZ.Authorization;

using Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Rule evaluation context for authorization decisions.
/// Carries all facts the RuleEngine needs to evaluate a permission.
/// </summary>
public sealed class AuthorizationRuleContext : IRuleContext
{
    private bool _halted;

    /// <summary>Identity of the user requesting access.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Tenant the user belongs to.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Resource being accessed (e.g. "orders", "invoices", "reports/monthly").</summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>Action being attempted (e.g. "read", "write", "delete", "approve").</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Roles assigned to the user from the identity token.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Additional claims from the identity token (e.g. department, cost_center).
    /// Available as facts for ABAC rule evaluation.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Claims { get; init; }
        = new Dictionary<string, object?>();

    /// <summary>
    /// Signals the rule engine to stop executing the current rule group.
    /// </summary>
    public void HaltGroup() => _halted = true;

    /// <summary>
    /// Indicates whether the current rule group has been halted.
    /// </summary>
    public bool IsHalted => _halted;
}
