namespace Quickstart.RuleEngine.Api.Models;

public sealed class OrderDecisionResponse
{
    public decimal OriginalAmount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal FinalAmount { get; set; }
    public string Decision { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Facts { get; set; } = new Dictionary<string, object?>();
}
