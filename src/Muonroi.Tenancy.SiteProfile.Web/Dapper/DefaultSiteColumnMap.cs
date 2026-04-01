using Muonroi.Core.Abstractions.Helpers;

namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Default ISiteColumnMap implementation that converts PascalCase C# property names
/// to UPPER_SNAKE_CASE database column names (the standard convention used across
/// Muonroi site databases).
///
/// Sites that differ only in a few column names subclass this and override just those columns:
/// <code>
/// public class MySiteColumnMap : DefaultSiteColumnMap
/// {
///     public override string Column(string propertyName)
///         => propertyName == "PropertyName" ? "COLUMN_NAME" : base.Column(propertyName);
/// }
/// </code>
/// </summary>
public class DefaultSiteColumnMap : ISiteColumnMap
{
    /// <inheritdoc />
    public virtual string Column(string propertyName)
        => ToUpperSnakeCase(propertyName);

    /// <inheritdoc cref="ISiteColumnMap.HasColumn"/>
    public virtual bool HasColumn(string propertyName) => true;

    /// <inheritdoc cref="ISiteColumnMap.ExtraColumns"/>
    public virtual IReadOnlyList<SiteExtraColumn> ExtraColumns => [];

    /// <summary>
    /// Converts a PascalCase string to UPPER_SNAKE_CASE.
    /// Delegates to <see cref="ColumnNamingConvention.ToUpperSnakeCase"/> — the single source of truth
    /// shared with EF Core configuration layer.
    /// </summary>
    protected static string ToUpperSnakeCase(string pascalCase)
        => ColumnNamingConvention.ToUpperSnakeCase(pascalCase);
}
