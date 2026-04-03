namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Holds EF IModel-derived column metadata for a single property, extracted at startup
/// by <see cref="EfColumnSyncHostedService"/>.
/// </summary>
/// <param name="ColumnName">The database column name from EF GetColumnName().</param>
/// <param name="MaxLength">The max string length from EF GetMaxLength(), or null if not configured.</param>
/// <param name="IsNullable">Whether the column allows NULL values from EF IsNullable.</param>
public sealed record SyncedColumnInfo(string ColumnName, int? MaxLength, bool IsNullable);
