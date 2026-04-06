namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Configuration options for <see cref="SiteProfileDbContextExtensions.AddSiteDbInfrastructure"/>.
/// Consumer declares HOW to resolve tenant identity, connection strings, and database provider
/// — the ecosystem handles the rest.
///
/// <example>
/// Finbuckle-based consumer:
/// <code>
/// services.AddSiteDbInfrastructure(o =>
/// {
///     o.TenantId = sp => sp.GetRequiredService&lt;IMultiTenantContextAccessor&lt;TenantInfo&gt;&gt;()
///                          .MultiTenantContext?.TenantInfo?.TenantId;
///     o.ConnectionString = sp => sp.GetRequiredService&lt;IMultiTenantContextAccessor&lt;TenantInfo&gt;&gt;()
///                                  .MultiTenantContext?.TenantInfo?.ConnectionString
///                               ?? throw new MInternalException("No tenant");
///     o.ConnectionStringTransform = cs => Cryptography.Decrypt(secretKey, cs);
/// });
/// </code>
///
/// Header-based consumer:
/// <code>
/// services.AddSiteDbInfrastructure(o =>
/// {
///     o.TenantId = sp => sp.GetRequiredService&lt;IHttpContextAccessor&gt;()
///                          .HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
///     o.ConnectionString = sp =>
///     {
///         var tenantId = sp.GetRequiredService&lt;ITenantContext&gt;().TenantId;
///         return sp.GetRequiredService&lt;IConfiguration&gt;()
///                  .GetConnectionString($"Tenant_{tenantId}") ?? throw new MInternalException();
///     };
///     o.ConfigureDbContext = (builder, cs) => builder.UseNpgsql(cs);
/// });
/// </code>
/// </example>
/// </summary>
public sealed class SiteDbInfrastructureOptions
{
    /// <summary>
    /// Resolves the current tenant identifier from the scoped service provider.
    /// Called once per scope (per HTTP request).
    ///
    /// Examples:
    /// - Finbuckle: <c>sp => accessor.MultiTenantContext?.TenantInfo?.TenantId</c>
    /// - HTTP header: <c>sp => httpContext.Request.Headers["X-Tenant-Id"]</c>
    /// - JWT claim: <c>sp => httpContext.User.FindFirst("tenant_id")?.Value</c>
    /// - Static/test: <c>sp => "default-tenant"</c>
    /// </summary>
    public Func<IServiceProvider, string?>? TenantId { get; set; }

    /// <summary>
    /// Resolves the raw connection string for the current tenant from the scoped service provider.
    /// Called once per scope (per HTTP request), after <see cref="TenantId"/> is resolved.
    ///
    /// The returned string may be encrypted — apply <see cref="ConnectionStringTransform"/> to decrypt.
    ///
    /// Examples:
    /// - Finbuckle: <c>sp => tenantInfo.ConnectionString</c>
    /// - Config-based: <c>sp => config.GetConnectionString($"Tenant_{tenantId}")</c>
    /// - External service: <c>sp => vaultClient.GetSecret($"db/{tenantId}")</c>
    /// </summary>
    public Func<IServiceProvider, string>? ConnectionString { get; set; }

    /// <summary>
    /// Optional transform applied to the raw connection string before use.
    /// Common use case: decrypting encrypted connection strings stored in tenant metadata.
    ///
    /// When null, the raw connection string from <see cref="ConnectionString"/> is used as-is.
    ///
    /// Example: <c>cs => Cryptography.Decrypt(secretKey, cs)</c>
    /// </summary>
    public Func<string, string>? ConnectionStringTransform { get; set; }

    /// <summary>
    /// Configures the database provider and options for per-site DbContexts.
    /// Receives <c>(builder, connectionString)</c> — call the appropriate provider method.
    ///
    /// When null, defaults to <c>builder.UseSqlServer(connectionString)</c>.
    ///
    /// Examples:
    /// - SQL Server: <c>(b, cs) => b.UseSqlServer(cs)</c>
    /// - PostgreSQL: <c>(b, cs) => b.UseNpgsql(cs)</c>
    /// - SQLite: <c>(b, cs) => b.UseSqlite(cs)</c>
    /// - SQL Server with options: <c>(b, cs) => b.UseSqlServer(cs, o => o.UseCompatibilityLevel(120))</c>
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureDbContext { get; set; }
}
