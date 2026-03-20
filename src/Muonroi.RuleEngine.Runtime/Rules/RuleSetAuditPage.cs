namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Represents a paged set of ruleset audit entries.
/// </summary>
public sealed class RuleSetAuditPage
{
    /// <summary>Gets or sets the page number.</summary>
    public int Page { get; init; }

    /// <summary>Gets or sets the page size.</summary>
    public int PageSize { get; init; }

    /// <summary>Gets or sets the total item count.</summary>
    public int TotalCount { get; init; }

    /// <summary>Gets or sets the page items.</summary>
    public IReadOnlyList<RuleSetAuditEntry> Items { get; init; } = [];
}
