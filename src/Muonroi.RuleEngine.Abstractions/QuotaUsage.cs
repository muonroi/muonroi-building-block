namespace Muonroi.RuleEngine.Abstractions;

public sealed class QuotaUsage
{
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<QuotaType, int> CurrentUsage { get; set; } = [];
    public Dictionary<QuotaType, int> Limits { get; set; } = [];
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
