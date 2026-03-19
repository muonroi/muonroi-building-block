namespace Muonroi.UiEngine.Catalog.Persistence;

internal sealed class CatalogSnapshotEntity
{
    public Guid SnapshotId { get; set; }
    public string TenantId { get; set; } = "_global";
    public DateTime CapturedAt { get; set; }
    public string GraphJson { get; set; } = "{}";
    public int ApiCount { get; set; }
    public int RuleCount { get; set; }
    public int BindingCount { get; set; }
}
