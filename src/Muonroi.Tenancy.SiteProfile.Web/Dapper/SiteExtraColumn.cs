namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Defines an extra database column that exists only for a specific site
/// and is not part of the base entity property list.
///
/// Used with <see cref="ISiteColumnMap.ExtraColumns"/> to dynamically append
/// site-specific columns to SQL SELECT queries via <see cref="SiteSqlBuilder.SelectWithExtras"/>.
/// </summary>
/// <param name="PropertyName">The C# property name used as the Dapper alias (e.g., "TrackingRef").</param>
/// <param name="ColumnName">The database column name (e.g., "TRACKING_REF").</param>
/// <param name="ClrType">The CLR type of the column value (e.g., <c>typeof(string)</c>).</param>
/// <param name="IsNullable">Whether the column allows NULL values. Defaults to <c>true</c>.</param>
public sealed record SiteExtraColumn(
    string PropertyName,
    string ColumnName,
    Type ClrType,
    bool IsNullable = true);
