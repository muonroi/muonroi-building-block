namespace Muonroi.RuleEngine.CEP.Persistence;

internal sealed class CepConfigEntity
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = "_global";

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string WindowType { get; set; } = Muonroi.RuleEngine.CEP.WindowType.Sliding.ToString();

    public int WindowSizeSeconds { get; set; }

    public int TimeToLiveSeconds { get; set; }

    public string CorrelationKey { get; set; } = "default";

    public string MetadataJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
