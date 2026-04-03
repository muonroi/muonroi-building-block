namespace Muonroi.Tenancy.Abstractions;

/// <summary>
/// Marks a type as being scoped to a specific tenant.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// Gets the unique identifier of the tenant this object belongs to.
    /// </summary>
    string? TenantId { get; }
}
