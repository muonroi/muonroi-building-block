namespace Muonroi.RuleEngine.DecisionTable.Stores.Persistence;

internal sealed class DecisionTableRecordEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HitPolicy { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public int Version { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class DecisionTableVersionEntity
{
    public long Id { get; set; }
    public string TableId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

internal sealed class DecisionTableAuditEntity
{
    public long Id { get; set; }
    public string? TableId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? PayloadJson { get; set; }
}
