namespace Muonroi.Tenancy.Core;

/// <summary>
/// Ambient tenant context backed by <see cref="AsyncLocal{T}"/>.
/// </summary>
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> Current = new();

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
