namespace Muonroi.Tenancy.Abstractions;

/// <summary>
/// Null-object implementation of <see cref="ITenantContext"/> for single-tenant/non-tenant applications.
/// Returns null TenantId, effectively disabling tenant isolation filters.
/// </summary>
public sealed class NoOpTenantContext : ITenantContext
{
    /// <inheritdoc />
    public string? TenantId { get; set; }
}
