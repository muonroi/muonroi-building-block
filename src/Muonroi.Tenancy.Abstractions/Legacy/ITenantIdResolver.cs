

namespace Muonroi.Tenancy.Abstractions.Legacy;

public interface ITenantIdResolver
{
    Task<string?> ResolveTenantIdAsync(HttpContext context);
}
