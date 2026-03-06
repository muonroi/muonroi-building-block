namespace Muonroi.Tenancy.Abstractions;

public interface ITenantScoped
{
    string? TenantId { get; }
}
