namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Maps C# property names to database column names per site.
/// Default implementation uses PascalCase → UPPER_SNAKE_CASE.
/// Sites override only columns that differ from convention.
///
/// Register a custom implementation per site:
/// <code>
/// services.AddKeyedSingleton&lt;ISiteColumnMap, BravoColumnMap&gt;("BRAVO");
/// services.AddSiteResolvedService&lt;ISiteColumnMap&gt;();
/// </code>
/// </summary>
public interface ISiteColumnMap
{
    /// <summary>
    /// Returns the database column name for a given C# property name.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "BookingNo").</param>
    /// <returns>The database column name (e.g., "BOOKING_NO").</returns>
    string Column(string propertyName);
}
