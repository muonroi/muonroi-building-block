namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Maps C# property names to database column names per site.
/// Default implementation uses PascalCase → UPPER_SNAKE_CASE.
/// Sites override only columns that differ from convention.
///
/// Register a custom implementation per site:
/// <code>
/// services.AddKeyedSingleton&lt;ISiteColumnMap, MySiteColumnMap&gt;("MySite");
/// services.AddSiteResolvedService&lt;ISiteColumnMap&gt;();
/// </code>
/// </summary>
public interface ISiteColumnMap
{
    /// <summary>
    /// Returns the database column name for a given C# property name (table-agnostic).
    /// Used when the same property maps to the same column across all tables for this site.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "MyProperty").</param>
    /// <returns>The database column name (e.g., "COLUMN_NAME").</returns>
    string Column(string propertyName);

    /// <summary>
    /// Returns the database column name for a given C# property name within a specific table.
    /// Override this when the same property name maps to different column names in different tables.
    /// <para>
    /// Example: TCI site has <c>BookingNo</c> → <c>TCI_BOOKING_EXT</c> in <c>ORDER_DETAIL</c>,
    /// but <c>BookingNo</c> → <c>BOOKING_NO</c> (default convention) in <c>CHART_DATA</c>.
    /// </para>
    /// <code>
    /// public override string Column(string propertyName, string tableName)
    ///     => (propertyName, tableName) switch
    ///     {
    ///         ("BookingNo", "ORDER_DETAIL") => "TCI_BOOKING_EXT",
    ///         _ => Column(propertyName)  // fallback to table-agnostic
    ///     };
    /// </code>
    /// Default: delegates to <see cref="Column(string)"/> (table-agnostic — backward compatible).
    /// </summary>
    /// <param name="propertyName">The C# property name.</param>
    /// <param name="tableName">The database table name providing context for the mapping.</param>
    /// <returns>The database column name for this property in this specific table.</returns>
    string Column(string propertyName, string tableName) => Column(propertyName);

    /// <summary>
    /// Indicates whether the given property maps to a column that exists in this site's schema.
    /// Returns <c>false</c> for columns that should be excluded from queries for this site.
    ///
    /// Default: <c>true</c> (all columns exist) — backward compatible.
    /// </summary>
    /// <param name="propertyName">The C# property name to check.</param>
    /// <returns><c>true</c> if the column exists for this site; <c>false</c> to exclude it from queries.</returns>
    bool HasColumn(string propertyName) => true;

    /// <summary>
    /// Returns extra columns that exist only for this site and are not part of the base entity.
    /// These columns are appended to queries via <see cref="SiteSqlBuilder.SelectWithExtras"/>.
    ///
    /// Default: empty list — backward compatible.
    /// </summary>
    IReadOnlyList<SiteExtraColumn> ExtraColumns => [];
}
