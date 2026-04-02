namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Ambient tenant context backed by <see cref="AsyncLocal{T}"/>.
/// </summary>
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> _currentTenantId = new();

    /// <summary>
    /// When true, EF global query filters for ITenantScoped entities are bypassed.
    /// Delegates to the main TenantContext.AllowCrossTenantAccess.
    /// </summary>
    public static bool AllowCrossTenantAccess
    {
        get => Muonroi.Tenancy.Core.TenantContext.AllowCrossTenantAccess;
        set => Muonroi.Tenancy.Core.TenantContext.AllowCrossTenantAccess = value;
    }

    /// <summary>
    /// Gets or sets the tenant identifier for the current asynchronous flow.
    /// </summary>
    public string? TenantId
    {
        get => _currentTenantId.Value;
        set => _currentTenantId.Value = value;
    }

    /// <summary>
    /// Convenience property to access the current tenant identifier without DI.
    /// </summary>
    public static string? CurrentTenantId
    {
        get => _currentTenantId.Value;
        set => _currentTenantId.Value = value;
    }
}
