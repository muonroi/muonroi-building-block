namespace Muonroi.Tenancy.Abstractions.Legacy;

public interface ITenantContext
{
    /// <summary>
    /// Gets or sets the identifier of the current tenant.
    /// </summary>
    string? TenantId { get; set; }
}
