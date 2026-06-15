using Microsoft.AspNetCore.Mvc;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

namespace Quickstart.Tenancy.Api.Controllers;

/// <summary>
/// Exercises the Muonroi tenancy services directly (without requiring an authenticated
/// tenant claim): the AsyncLocal <see cref="ITenantContext"/>, the HTTP
/// <see cref="ITenantIdResolver"/>, the <see cref="TenantSchemaSelector"/>, and the
/// <see cref="ITenantConnectionStringFactory"/>.
/// </summary>
[ApiController]
[Route("api/tenant")]
public class TenantController(
    ITenantContext tenantContext,
    ITenantIdResolver resolver,
    TenantSchemaSelector schemaSelector,
    ITenantConnectionStringFactory connectionStringFactory) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Resolve the tenant id from the current request
    //    GET /api/tenant/resolve?X-Tenant-Id=...  (or header / route / subdomain)
    //
    //    DefaultTenantIdResolver inspects, in order: tenant claim, X-Tenant-Id header,
    //    route value, path segment, then subdomain.
    // ---------------------------------------------------------------------------
    [HttpGet("resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(CancellationToken token)
    {
        string? resolved = await resolver.ResolveTenantIdAsync(HttpContext);
        return Ok(new
        {
            resolvedTenantId = resolved,
            hint = "Send header 'X-Tenant-Id: acme' (CustomHeader.TenantId) to see it resolved."
        });
    }

    // ---------------------------------------------------------------------------
    // 2. Set + read the ambient tenant context
    //    POST /api/tenant/context?tenantId=acme
    //
    //    TenantContext is AsyncLocal — the value flows through the async call tree for
    //    the current request and is also visible via the static CurrentTenantId used
    //    by EF Core global query filters.
    // ---------------------------------------------------------------------------
    [HttpPost("context")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult SetContext([FromQuery] string tenantId)
    {
        tenantContext.TenantId = tenantId;
        return Ok(new
        {
            instanceTenantId = tenantContext.TenantId,        // via ITenantContext
            staticCurrentTenantId = TenantContext.CurrentTenantId // same AsyncLocal slot
        });
    }

    // ---------------------------------------------------------------------------
    // 3. Resolve a schema name for a tenant
    //    GET /api/tenant/schema?tenantId=acme-corp
    //
    //    With Strategy=SeparateSchema, TenantSchemaSelector maps the tenant id to a
    //    sanitized schema name (lowercased, '-'/'.'/' ' → '_'); otherwise it returns
    //    "dbo". ApplyToConnectionString appends SearchPath for PostgreSQL.
    // ---------------------------------------------------------------------------
    [HttpGet("schema")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ResolveSchema([FromQuery] string tenantId = "acme-corp")
    {
        string schema = schemaSelector.ResolveSchema(tenantId);
        string rewritten = schemaSelector.ApplyToConnectionString(
            "Host=localhost;Database=quickstart;Username=app;Password=app", tenantId);

        return Ok(new
        {
            tenantId,
            schema,
            connectionStringWithSchema = rewritten,
            note = "Schema mapping only applies when MultiTenantConfigs:Strategy = SeparateSchema."
        });
    }

    // ---------------------------------------------------------------------------
    // 4. Resolve a per-tenant connection string
    //    GET /api/tenant/connection-string?tenantId=acme
    //
    //    MappingTenantConnectionStringFactory looks the tenant id up in
    //    TenantConnectionStrings:ConnectionStrings and falls back to the default.
    // ---------------------------------------------------------------------------
    [HttpGet("connection-string")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetConnectionString([FromQuery] string? tenantId)
    {
        string conn = connectionStringFactory.GetConnectionString(tenantId);
        return Ok(new
        {
            tenantId,
            connectionString = conn,
            note = "Configured tenants resolve to their mapped string; unknown/blank falls back to default."
        });
    }
}
