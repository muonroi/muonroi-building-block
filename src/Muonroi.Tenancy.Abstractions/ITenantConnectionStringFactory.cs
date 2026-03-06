namespace Muonroi.Tenancy.Abstractions;

public interface ITenantConnectionStringFactory
{
    string GetConnectionString(string? tenantId);
}
