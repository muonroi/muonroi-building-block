namespace TestProject.Service.Core.Constants;

/// <summary>
/// Site identifier constants - maps to TenantConfigs.Code in appsettings.json.
/// </summary>
public static class SiteIds
{
    public const string DEFAULT = "DEFAULT";
    public const string ALPHA = "ALPHA";
    public const string BRAVO = "BRAVO";

    /// <summary>CHARLIE is an alias for DEFAULT — uses DEFAULT's service registrations.</summary>
    public const string CHARLIE = "CHARLIE";
}
