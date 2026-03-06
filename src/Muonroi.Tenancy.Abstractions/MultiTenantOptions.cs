namespace Muonroi.Tenancy.Abstractions;

public enum TenantIsolationStrategy
{
    SharedSchema,
    SeparateSchema,
    SeparateDatabase
}

public class MultiTenantOptions
{
    public const string SectionName = "MultiTenantConfigs";
    public bool Enabled { get; set; } = true;
    public bool RequireTenantClaimForAuthenticatedUser { get; set; } = true;
    public TenantIsolationStrategy Strategy { get; set; } = TenantIsolationStrategy.SharedSchema;
}
