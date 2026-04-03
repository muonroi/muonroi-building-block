namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Marks a context as belonging to a specific tenant.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// Identifier of the tenant that owns this context.
    /// </summary>
    string? TenantId { get; }
}
