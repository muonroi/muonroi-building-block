namespace TestProject.Aggregate.Core.Constants;

/// <summary>
/// Site identifier constants for the 3 test tenants.
/// </summary>
public static class SiteIds
{
    /// <summary>
    /// The default site identifier, used for the first tenant. This is a common convention to have a "DEFAULT" site for the primary tenant, and then additional sites with unique identifiers (e.g. "ALPHA", "BRAVO") for other tenants. This allows for easy identification and routing based on site ID in multi-tenant applications.
    /// </summary>
    public const string DEFAULT = "DEFAULT";
    /// <summary>
    /// Represents the string value "ALPHA".
    /// </summary>
    public const string ALPHA = "ALPHA";
    /// <summary>
    /// Represents the string value "BRAVO".
    /// </summary>
    public const string BRAVO = "BRAVO";
}
