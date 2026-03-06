namespace Muonroi.Tenancy.Core;

public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public string? TenantId
    {
        get => Current.Value;
        set => Current.Value = value;
    }

    public static string? CurrentTenantId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
