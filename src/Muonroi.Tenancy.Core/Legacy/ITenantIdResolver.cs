

namespace Muonroi.Tenancy.Core.Legacy;

public interface ITenantIdResolver
{
    Task<string?> ResolveTenantIdAsync(HttpContext context);
}
