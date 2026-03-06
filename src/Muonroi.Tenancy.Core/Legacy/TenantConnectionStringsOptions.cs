namespace Muonroi.Tenancy.Core.Legacy;

public class TenantConnectionStringsOptions
{
    public const string SectionName = "TenantConnectionStrings";
    public Dictionary<string, string> ConnectionStrings { get; set; } = [];
}
