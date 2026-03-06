namespace Muonroi.Tenancy.Abstractions;

public class TenantConnectionStringsOptions
{
    public const string SectionName = "TenantConnectionStrings";
    public Dictionary<string, string> ConnectionStrings { get; set; } = [];
}
