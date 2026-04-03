namespace Muonroi.AuthZ.RowSecurity;

using Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Context passed to row-filter rules. Rules modify <see cref="Query"/> in place
/// to apply per-user/per-tenant restrictions.
/// </summary>
public sealed class RowFilterContext<T> : IRuleContext
{
    private bool _halted;

    /// <summary>
    /// Gets the identity of the user requesting the data.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant identifier for the current request.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the roles assigned to the current user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// The queryable that rules filter. Rules MUST set this to a filtered version.
    /// </summary>
    public IQueryable<T> Query { get; set; } = Enumerable.Empty<T>().AsQueryable();

    /// <summary>
    /// Signals the rule engine to stop executing the current rule group.
    /// </summary>
    public void HaltGroup() => _halted = true;

    /// <summary>
    /// Indicates whether the current rule group has been halted.
    /// </summary>
    public bool IsHalted => _halted;
}
