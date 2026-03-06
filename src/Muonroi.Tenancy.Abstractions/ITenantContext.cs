namespace Muonroi.Tenancy.Abstractions;

public interface ITenantContext
{
    string? TenantId { get; set; }
}
