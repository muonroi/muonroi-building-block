namespace Muonroi.Tenancy.Core.Legacy;

public class MultiTenantConfigs
{
    public const string SectionName = "MultiTenantConfigs";
    public static string Section => SectionName;
    public bool Enabled { get; set; } = true;
    public bool RequireTenantClaimForAuthenticatedUser { get; set; } = true;
}
