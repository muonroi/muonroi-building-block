namespace Muonroi.Tenancy.Core.Legacy;

public interface ITenantConnectionStringFactory
{
    string GetConnectionString(string? tenantId);
}
