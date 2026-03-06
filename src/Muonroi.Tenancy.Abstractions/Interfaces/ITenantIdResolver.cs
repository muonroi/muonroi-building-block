using Microsoft.AspNetCore.Http;

namespace Muonroi.Tenancy.Abstractions.Interfaces;

public interface ITenantIdResolver
{
    Task<string?> ResolveTenantIdAsync(HttpContext context);
}
