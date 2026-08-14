namespace Quickstart.RuleEngine.Core.Api.Models;

public class OrderContext : IRuleContext
{
    public string OrderId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public bool IsPremiumCustomer { get; set; }
    public void HaltGroup() { }
}
