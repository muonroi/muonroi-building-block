namespace Muonroi.UiEngine.Catalog.Options;

/// <summary>
/// Configuration for UI engine catalog scanning and snapshot persistence.
/// </summary>
public sealed class UiEngineCatalogOptions
{
    /// <summary>
    /// The PostgreSQL connection string for persisted catalog snapshots.
    /// </summary>
    public string? PostgresConnectionString { get; set; }

    /// <summary>
    /// The SQL Server connection string for persisted catalog snapshots.
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>
    /// The database schema used by the snapshot store.
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>
    /// Whether the package should automatically migrate the snapshot database.
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = true;
}
