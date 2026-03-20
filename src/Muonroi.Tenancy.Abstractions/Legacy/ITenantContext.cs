namespace Muonroi.Tenancy.Abstractions.Legacy;

/// <summary>
/// Provides access to the current tenant context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets or sets the identifier of the current tenant.
    /// </summary>
    string? TenantId { get; set; }
}
