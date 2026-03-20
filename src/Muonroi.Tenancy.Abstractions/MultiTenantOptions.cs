namespace Muonroi.Tenancy.Abstractions;

/// <summary>
/// Describes how tenant data is isolated.
/// </summary>
public enum TenantIsolationStrategy
{
    /// <summary>
    /// Share a single schema across tenants.
    /// </summary>
    SharedSchema,
    /// <summary>
    /// Use a separate schema per tenant.
    /// </summary>
    SeparateSchema,
    /// <summary>
    /// Use a separate database per tenant.
    /// </summary>
    SeparateDatabase
}

/// <summary>
/// Configuration options for multi-tenant behavior.
/// </summary>
public class MultiTenantOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "MultiTenantConfigs";

    /// <summary>
    /// Gets or sets a value indicating whether multi-tenant features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an authenticated user must include a tenant claim.
    /// </summary>
    public bool RequireTenantClaimForAuthenticatedUser { get; set; } = true;

    /// <summary>
    /// Gets or sets the tenant isolation strategy.
    /// </summary>
    public TenantIsolationStrategy Strategy { get; set; } = TenantIsolationStrategy.SharedSchema;
}
