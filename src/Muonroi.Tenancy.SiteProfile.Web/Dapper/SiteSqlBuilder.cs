using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Generates SQL SELECT clauses with correct column aliases per site.
///
/// Avoids the process-global <c>SqlMapper.SetTypeMap()</c> Dapper API (which would
/// affect ALL queries across ALL sites in the same process). Instead, each site injects
/// its <see cref="ISiteColumnMap"/> and this builder generates explicit "COLUMN AS Property"
/// aliases per query — Dapper maps aliases to properties by name automatically.
///
/// Usage:
/// <code>
/// // Inject per-site builder from DI:
/// // services.AddScoped&lt;SiteSqlBuilder&gt;(); // resolved after ISiteColumnMap via site DI
///
/// string sql = builder.SelectFrom("my_table", "MyProperty", "MyOtherProperty", "CreatedAt");
/// // → "SELECT MY_PROPERTY AS MyProperty, MY_OTHER_PROPERTY AS MyOtherProperty, CREATED_AT AS CreatedAt FROM my_table"
/// var results = await dapper.QueryAsync&lt;MyDto&gt;(sql, parameters);
/// </code>
/// </summary>
public sealed partial class SiteSqlBuilder
{
    private readonly ISiteColumnMap _columnMap;

    /// <summary>
    /// Initializes a new <see cref="SiteSqlBuilder"/> with the site-specific column map.
    /// </summary>
    /// <param name="columnMap">The column map for the current site. Cannot be null.</param>
    public SiteSqlBuilder(ISiteColumnMap columnMap)
    {
        MGuard.NotNull(columnMap);
        _columnMap = columnMap;
    }

    /// <summary>
    /// Generates a comma-separated list of "COLUMN_NAME AS PropertyName" pairs for each property.
    /// </summary>
    /// <param name="propertyNames">The C# property names to select. Must not be empty.</param>
    /// <returns>SQL column list, e.g. "BOOKING_NO AS BookingNo, CONTAINER_NO AS ContainerNo".</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="propertyNames"/> is empty.</exception>
    public string Select(params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
            MGuard.Against(propertyNames.Length == 0, "At least one property name required.");

        var filtered = propertyNames.Where(p => _columnMap.HasColumn(p)).ToArray();
        if (filtered.Length == 0)
            throw new MArgumentException(nameof(propertyNames), "All property names were filtered out by HasColumn. At least one column must be selectable.");

        return string.Join(", ", filtered.Select(p =>
        {
            string columnName = _columnMap.Column(p);
            ColumnNameValidator.EnsureValidIdentifier(columnName, p);
            return $"{columnName} AS {p}";
        }));
    }

    /// <summary>
    /// Generates a full <c>SELECT columns FROM tableName</c> statement.
    /// </summary>
    /// <param name="tableName">The database table name.</param>
    /// <param name="propertyNames">The C# property names to select. Must not be empty.</param>
    /// <returns>Complete SQL SELECT statement.</returns>
    public string SelectFrom(string tableName, params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
            MGuard.Against(propertyNames.Length == 0, "At least one property name required.");

        var filtered = propertyNames.Where(p => _columnMap.HasColumn(p)).ToArray();
        if (filtered.Length == 0)
            throw new MArgumentException(nameof(propertyNames), "All property names were filtered out by HasColumn. At least one column must be selectable.");

        var cols = string.Join(", ", filtered.Select(p =>
        {
            string columnName = _columnMap.Column(p, tableName);
            ColumnNameValidator.EnsureValidIdentifier(columnName, p);
            return $"{columnName} AS {p}";
        }));
        return $"SELECT {cols} FROM {tableName}";
    }

    /// <summary>
    /// Generates a comma-separated column list that includes both base properties (filtered
    /// by <see cref="ISiteColumnMap.HasColumn"/>) and site-specific extra columns from
    /// <see cref="ISiteColumnMap.ExtraColumns"/>.
    ///
    /// Use this when the site may have additional columns not present in the base entity.
    /// </summary>
    /// <param name="propertyNames">The base C# property names to select.</param>
    /// <returns>SQL column list including extras, e.g. "BOOKING_NO AS BookingNo, TRACKING_REF AS TrackingRef".</returns>
    /// <exception cref="ArgumentException">Thrown when no columns remain after filtering and no extras are defined.</exception>
    public string SelectWithExtras(params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
            MGuard.Against(propertyNames.Length == 0, "At least one property name required.");

        var baseCols = propertyNames
            .Where(p => _columnMap.HasColumn(p))
            .Select(p =>
            {
                string columnName = _columnMap.Column(p);
                ColumnNameValidator.EnsureValidIdentifier(columnName, p);
                return $"{columnName} AS {p}";
            });

        var extraCols = _columnMap.ExtraColumns
            .Select(e =>
            {
                ColumnNameValidator.EnsureValidIdentifier(e.ColumnName, e.PropertyName);
                return $"{e.ColumnName} AS {e.PropertyName}";
            });

        var all = baseCols.Concat(extraCols).ToArray();
        if (all.Length == 0)
            throw new MArgumentException(nameof(propertyNames), "No columns available: all base properties filtered and no extra columns defined.");

        return string.Join(", ", all);
    }

    /// <summary>
    /// Generates a full <c>SELECT columns FROM tableName</c> statement including
    /// site-specific extra columns.
    /// </summary>
    /// <param name="tableName">The database table name.</param>
    /// <param name="propertyNames">The base C# property names to select.</param>
    /// <returns>Complete SQL SELECT statement with extras.</returns>
    public string SelectFromWithExtras(string tableName, params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
            MGuard.Against(propertyNames.Length == 0, "At least one property name required.");

        var baseCols = propertyNames
            .Where(p => _columnMap.HasColumn(p))
            .Select(p =>
            {
                string columnName = _columnMap.Column(p, tableName);
                ColumnNameValidator.EnsureValidIdentifier(columnName, p);
                return $"{columnName} AS {p}";
            });

        var extraCols = _columnMap.ExtraColumns
            .Select(e =>
            {
                ColumnNameValidator.EnsureValidIdentifier(e.ColumnName, e.PropertyName);
                return $"{e.ColumnName} AS {e.PropertyName}";
            });

        var all = baseCols.Concat(extraCols).ToArray();
        if (all.Length == 0)
            throw new MArgumentException(nameof(propertyNames), "No columns available: all base properties filtered and no extra columns defined.");

        return $"SELECT {string.Join(", ", all)} FROM {tableName}";
    }

    /// <summary>
    /// Returns the site-specific column name for a C# property.
    /// Convenience wrapper for <see cref="ISiteColumnMap.Column(string)"/> — useful in
    /// WHERE/JOIN clauses where <see cref="Interpolate"/> cannot auto-detect columns.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "BookingNo").</param>
    /// <returns>The database column name (e.g., "BOOKING_NO" or site-specific override).</returns>
    public string Col(string propertyName)
    {
        string columnName = _columnMap.Column(propertyName);
        ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
        return columnName;
    }

    /// <summary>
    /// Returns the site-specific column name for a C# property within a specific table.
    /// Use when the same property maps differently across tables.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "BookingNo").</param>
    /// <param name="tableName">The database table name providing context for the mapping.</param>
    /// <returns>The database column name for this property in this specific table.</returns>
    public string Col(string propertyName, string tableName)
    {
        string columnName = _columnMap.Column(propertyName, tableName);
        ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
        return columnName;
    }

    /// <summary>
    /// Returns the site-specific column name within a table context if the column exists,
    /// or <c>null</c> if removed via <see cref="ISiteColumnMap.HasColumn"/>.
    /// </summary>
    /// <param name="propertyName">The C# property name to check.</param>
    /// <param name="tableName">The database table name providing context for the mapping.</param>
    /// <returns>The database column name, or <c>null</c> if the column doesn't exist for this site.</returns>
    public string? ColOrNull(string propertyName, string tableName)
    {
        if (!_columnMap.HasColumn(propertyName))
            return null;
        string columnName = _columnMap.Column(propertyName, tableName);
        ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
        return columnName;
    }

    /// <summary>
    /// Returns the site-specific column name if the column exists for this site,
    /// or <c>null</c> if the column has been removed via <see cref="ISiteColumnMap.HasColumn"/>.
    ///
    /// Use this to conditionally include columns in WHERE/JOIN clauses:
    /// <code>
    /// var col = builder.ColOrNull("ContainerNo");
    /// string where = col is not null ? $"AND {col} = @containerNo" : "";
    /// </code>
    /// </summary>
    /// <param name="propertyName">The C# property name to check.</param>
    /// <returns>The database column name, or <c>null</c> if the column doesn't exist for this site.</returns>
    public string? ColOrNull(string propertyName)
    {
        if (!_columnMap.HasColumn(propertyName))
            return null;

        string columnName = _columnMap.Column(propertyName);
        ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
        return columnName;
    }

    /// <summary>
    /// Checks whether a column exists for the current site.
    /// Convenience wrapper for <see cref="ISiteColumnMap.HasColumn"/>.
    /// </summary>
    /// <param name="propertyName">The C# property name to check.</param>
    /// <returns><c>true</c> if the column exists; <c>false</c> if it has been removed for this site.</returns>
    public bool HasColumn(string propertyName) => _columnMap.HasColumn(propertyName);

    /// <summary>
    /// Interpolates a raw SQL string by replacing column names in <c>[alias.]COLUMN AS Property</c>
    /// patterns with the site-specific column name from <see cref="ISiteColumnMap"/>.
    ///
    /// <para>
    /// Only replaces simple column references with a table alias prefix (e.g., <c>od.ITEM_NO AS ContainerNo</c>).
    /// Complex expressions (CASE, functions, subqueries) are left unchanged because they lack
    /// a <c>tableAlias.columnName</c> prefix.
    /// </para>
    ///
    /// <para>
    /// For the default <see cref="DefaultSiteColumnMap"/> (PascalCase → UPPER_SNAKE_CASE),
    /// output is identical to input — zero behavioral change. Sites that override specific
    /// column mappings get automatic SQL rewriting without touching query strings.
    /// </para>
    ///
    /// Usage:
    /// <code>
    /// // Raw SQL with hardcoded column names:
    /// const string sql = "SELECT od.ITEM_NO AS ContainerNo, od.BOOKING_NO AS BookingNo FROM ORDER_DETAIL od";
    ///
    /// // Interpolated — column names resolved per site:
    /// string siteSql = builder.Interpolate(sql);
    /// // Default site: "SELECT od.ITEM_NO AS ContainerNo, od.BOOKING_NO AS BookingNo FROM ORDER_DETAIL od"
    /// // TCI site:     "SELECT od.ITEM_NO AS ContainerNo, od.TCI_BOOKING_EXT AS BookingNo FROM ORDER_DETAIL od"
    /// </code>
    /// </summary>
    /// <param name="rawSql">The raw SQL string to interpolate. Must not be null.</param>
    /// <returns>The SQL string with column names replaced per the current site's column map.</returns>
    /// <remarks>
    /// This method is obsolete. Use <see cref="InterpolateMarkers"/> with <c>[[PropertyName]]</c> markers instead.
    /// </remarks>
    [Obsolete("Use InterpolateMarkers with [[PropertyName]] markers instead. Interpolate() rewrites alias.COLUMN AS Property patterns which is fragile for complex queries.")]
    public string Interpolate(string rawSql)
    {
        MGuard.NotNull(rawSql);

        // Pattern: [tableAlias.]COLUMN_NAME AS PropertyName
        // - Requires table alias prefix to avoid false matches on CASE...END, functions, literals
        // - COLUMN_NAME: word characters (letters, digits, underscore)
        // - AS: case-insensitive keyword
        // - PropertyName: starts with uppercase letter, contains at least one lowercase (PascalCase)
        return InterpolateRegex().Replace(rawSql, match =>
        {
            string tableAlias = match.Groups[1].Value; // e.g., "od."
            string propertyName = match.Groups[3].Value; // e.g., "ContainerNo"
            if (!_columnMap.HasColumn(propertyName))
                return match.Value; // Leave original SQL unchanged for unmapped properties
            string siteColumn = _columnMap.Column(propertyName);
            ColumnNameValidator.EnsureValidIdentifier(siteColumn, propertyName);
            return $"{tableAlias}{siteColumn} AS {propertyName}";
        });
    }

    /// <summary>
    /// Interpolates a raw SQL string by replacing <c>[[PropertyName]]</c> markers with the
    /// site-specific column name from <see cref="ISiteColumnMap"/>.
    ///
    /// <para>
    /// Use this method for complex SQL patterns that cannot be handled by <see cref="Interpolate"/>:
    /// WHERE clauses, JOIN ON conditions, CASE expressions, CTEs, GROUP BY clauses, or any
    /// SQL fragment that does not follow the <c>alias.COLUMN AS Property</c> pattern.
    /// </para>
    ///
    /// <para>
    /// The <c>[[]]</c> syntax is preferred over the legacy <c>{{}}</c> syntax because
    /// double-braces have special meaning in C# interpolated strings ($"..."), requiring ugly
    /// <c>{{{{PropertyName}}}}</c> escaping. Square brackets have no special meaning in C#,
    /// raw strings, or T-SQL, making them developer-friendly in any string context.
    /// </para>
    ///
    /// Usage:
    /// <code>
    /// // Raw string literal (no escaping needed):
    /// const string sql = """SELECT [[BookingNo]] FROM orders""";
    ///
    /// // Interpolated string combining markers with runtime values:
    /// string sql = $"WHERE [[BookingNo]] = @bookingNo AND od.{someVar}";
    ///
    /// // Markers resolved per site:
    /// string siteSql = builder.InterpolateMarkers(sql);
    /// // Default site: "SELECT BOOKING_NO FROM orders"
    /// // TCI site:     "SELECT TCI_BOOKING_EXT FROM orders"
    /// </code>
    /// </summary>
    /// <param name="rawSql">The raw SQL string containing <c>[[PropertyName]]</c> markers. Must not be null.</param>
    /// <returns>The SQL string with all <c>[[PropertyName]]</c> markers replaced by site-specific column names.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a <c>[[PropertyName]]</c> marker references a column that does not exist
    /// for this site (i.e., <see cref="ISiteColumnMap.HasColumn"/> returns <c>false</c>).
    /// Use <see cref="InterpolateMarkersSafe"/> to replace removed columns with a fallback value instead.
    /// </exception>
    public string InterpolateMarkers(string rawSql)
    {
        MGuard.NotNull(rawSql);
        return MarkerRegex().Replace(rawSql, match =>
        {
            string propertyName = match.Groups[1].Value;
            if (!_columnMap.HasColumn(propertyName))
                throw new MInternalException(
                    $"Column '{propertyName}' is not available for this site. " +
                    $"Use InterpolateMarkersSafe() or check HasColumn(\"{propertyName}\") before using this marker.");
            string columnName = _columnMap.Column(propertyName);
            ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
            return columnName;
        });
    }

    /// <summary>
    /// Interpolates <c>[[PropertyName]]</c> markers like <see cref="InterpolateMarkers"/>,
    /// but replaces markers for removed columns with a fallback value instead of throwing.
    ///
    /// <para>
    /// When <see cref="ISiteColumnMap.HasColumn"/> returns <c>false</c> for a marker's property,
    /// the entire <c>[[PropertyName]]</c> marker is replaced with <paramref name="fallback"/>.
    /// </para>
    ///
    /// Usage:
    /// <code>
    /// // Site where ContainerNo is removed:
    /// string sql = builder.InterpolateMarkersSafe("SELECT [[BookingNo]], [[ContainerNo]] FROM orders");
    /// // → "SELECT BOOKING_NO, NULL FROM orders"
    ///
    /// // With custom fallback:
    /// string sql = builder.InterpolateMarkersSafe("SELECT [[ContainerNo]] FROM orders", "'N/A'");
    /// // → "SELECT 'N/A' FROM orders"
    /// </code>
    /// </summary>
    /// <param name="rawSql">The raw SQL string containing <c>[[PropertyName]]</c> markers.</param>
    /// <param name="fallback">The SQL literal to use for removed columns. Defaults to <c>"NULL"</c>.</param>
    /// <returns>The SQL string with markers resolved or replaced with fallback.</returns>
    public string InterpolateMarkersSafe(string rawSql, string fallback = "NULL")
    {
        MGuard.NotNull(rawSql);
        return MarkerRegex().Replace(rawSql, match =>
        {
            string propertyName = match.Groups[1].Value;
            if (!_columnMap.HasColumn(propertyName))
                return fallback;
            string columnName = _columnMap.Column(propertyName);
            ColumnNameValidator.EnsureValidIdentifier(columnName, propertyName);
            return columnName;
        });
    }

    // Matches: od.COLUMN_NAME AS PropertyName
    // Group 1: table alias with dot (e.g., "od.")
    // Group 2: original column name (e.g., "ITEM_NO")
    // Group 3: property alias in PascalCase (e.g., "ContainerNo")
    [System.Text.RegularExpressions.GeneratedRegex(
        @"(\b\w+\.)(\w+)\s+AS\s+([A-Z][a-zA-Z0-9]+)",
        System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex InterpolateRegex();

    // Matches: [[PropertyName]] markers
    // Group 1: property name (e.g., "SiteCode", "BookingNo")
    [System.Text.RegularExpressions.GeneratedRegex(
        @"\[\[(\w+)\]\]",
        System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex MarkerRegex();
}
