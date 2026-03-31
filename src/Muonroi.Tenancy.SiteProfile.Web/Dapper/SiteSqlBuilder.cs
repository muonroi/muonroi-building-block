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
        ArgumentNullException.ThrowIfNull(columnMap);
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
            throw new ArgumentException("At least one property name required.", nameof(propertyNames));

        return string.Join(", ", propertyNames.Select(p => $"{_columnMap.Column(p)} AS {p}"));
    }

    /// <summary>
    /// Generates a full <c>SELECT columns FROM tableName</c> statement.
    /// </summary>
    /// <param name="tableName">The database table name.</param>
    /// <param name="propertyNames">The C# property names to select. Must not be empty.</param>
    /// <returns>Complete SQL SELECT statement.</returns>
    public string SelectFrom(string tableName, params string[] propertyNames)
        => $"SELECT {Select(propertyNames)} FROM {tableName}";

    /// <summary>
    /// Returns the site-specific column name for a C# property.
    /// Convenience wrapper for <see cref="ISiteColumnMap.Column"/> — useful in
    /// WHERE/JOIN clauses where <see cref="Interpolate"/> cannot auto-detect columns.
    /// </summary>
    /// <param name="propertyName">The C# property name (e.g., "BookingNo").</param>
    /// <returns>The database column name (e.g., "BOOKING_NO" or site-specific override).</returns>
    public string Col(string propertyName) => _columnMap.Column(propertyName);

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
    public string Interpolate(string rawSql)
    {
        ArgumentNullException.ThrowIfNull(rawSql);

        // Pattern: [tableAlias.]COLUMN_NAME AS PropertyName
        // - Requires table alias prefix to avoid false matches on CASE...END, functions, literals
        // - COLUMN_NAME: word characters (letters, digits, underscore)
        // - AS: case-insensitive keyword
        // - PropertyName: starts with uppercase letter, contains at least one lowercase (PascalCase)
        return InterpolateRegex().Replace(rawSql, match =>
        {
            string tableAlias = match.Groups[1].Value; // e.g., "od."
            string propertyName = match.Groups[3].Value; // e.g., "ContainerNo"
            string siteColumn = _columnMap.Column(propertyName);
            return $"{tableAlias}{siteColumn} AS {propertyName}";
        });
    }

    /// <summary>
    /// Interpolates a raw SQL string by replacing <c>{{PropertyName}}</c> markers with the
    /// site-specific column name from <see cref="ISiteColumnMap"/>.
    ///
    /// <para>
    /// Use this method for complex SQL patterns that cannot be handled by <see cref="Interpolate"/>:
    /// WHERE clauses, JOIN ON conditions, CASE expressions, CTEs, GROUP BY clauses, or any
    /// SQL fragment that does not follow the <c>alias.COLUMN AS Property</c> pattern.
    /// </para>
    ///
    /// Usage:
    /// <code>
    /// // Raw SQL with explicit markers:
    /// const string sql = "WHERE od.{{SiteCode}} = @siteCode AND {{BookingNo}} = @bookingNo";
    ///
    /// // Interpolated — markers resolved per site:
    /// string siteSql = builder.InterpolateMarkers(sql);
    /// // Default site: "WHERE od.SITE_CODE = @siteCode AND BOOKING_NO = @bookingNo"
    /// // TCI site:     "WHERE od.SITE_CODE = @siteCode AND TCI_BOOKING_EXT = @bookingNo"
    /// </code>
    /// </summary>
    /// <param name="rawSql">The raw SQL string containing <c>{{PropertyName}}</c> markers. Must not be null.</param>
    /// <returns>The SQL string with all <c>{{PropertyName}}</c> markers replaced by site-specific column names.</returns>
    public string InterpolateMarkers(string rawSql)
    {
        ArgumentNullException.ThrowIfNull(rawSql);
        return MarkerRegex().Replace(rawSql, match =>
        {
            string propertyName = match.Groups[1].Value;
            return _columnMap.Column(propertyName);
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

    // Matches: {{PropertyName}} markers
    // Group 1: property name (e.g., "SiteCode", "BookingNo")
    [System.Text.RegularExpressions.GeneratedRegex(
        @"\{\{(\w+)\}\}",
        System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex MarkerRegex();
}
