namespace Muonroi.Tenancy.Abstractions;

/// <summary>
/// Provides access to the current tenant context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets or sets the unique identifier of the current tenant.
    /// </summary>
    string? TenantId { get; set; }
}
