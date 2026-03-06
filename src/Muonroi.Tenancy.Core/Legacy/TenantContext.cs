namespace Muonroi.Tenancy.Core.Legacy;

public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> _currentTenantId = new();

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
