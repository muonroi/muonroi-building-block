namespace Quickstart.RuleEngine.Api.Models;

public sealed class OrderRequest
{
    public decimal Amount { get; set; }
    public string CustomerType { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}
