namespace Muonroi.Tenancy.Core;

/// <summary>
/// Ambient tenant context backed by <see cref="AsyncLocal{T}"/>.
/// </summary>
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> Current = new();
    private static readonly AsyncLocal<bool> _allowCrossTenant = new();

    /// <summary>
    /// When true, EF global query filters for ITenantScoped entities are bypassed.
    /// Use only for admin/system operations that legitimately need cross-tenant access.
    /// Default: false (fail-closed).
    /// </summary>
    public static bool AllowCrossTenantAccess
    {
        get => _allowCrossTenant.Value;
        set => _allowCrossTenant.Value = value;
    }

    /// <inheritdoc />
    public string? TenantId
    {
        get => Current.Value;
        set => Current.Value = value;
    }

    /// <summary>
    /// Gets or sets the current tenant identifier for the ambient context.
    /// </summary>
    public static string? CurrentTenantId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
