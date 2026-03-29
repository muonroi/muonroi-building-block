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
/// string sql = builder.SelectFrom("bookings", "BookingNo", "ContainerNo", "CreatedAt");
/// // → "SELECT BOOKING_NO AS BookingNo, CONTAINER_NO AS ContainerNo, CREATED_AT AS CreatedAt FROM bookings"
/// var results = await dapper.QueryAsync&lt;BookingDto&gt;(sql, parameters);
/// </code>
/// </summary>
public sealed class SiteSqlBuilder
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
}
