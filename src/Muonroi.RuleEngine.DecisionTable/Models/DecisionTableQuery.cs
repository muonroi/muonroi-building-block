namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? TenantId { get; set; }
    public HitPolicy? HitPolicy { get; set; }
    public bool IncludeDeleted { get; set; }
}
