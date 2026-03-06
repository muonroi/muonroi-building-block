namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetAuditPage
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<RuleSetAuditEntry> Items { get; init; } = [];
}
