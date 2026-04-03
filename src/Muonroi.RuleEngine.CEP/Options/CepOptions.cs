namespace Muonroi.RuleEngine.CEP.Options;

/// <summary>
/// Configures CEP persistence and schema options.
/// </summary>
public sealed class CepOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL connection string.
    /// </summary>
    public string? PostgresConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server connection string.
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database schema.
    /// </summary>
    public string? Schema { get; set; } = "dbo";

    /// <summary>
    /// Gets or sets a value indicating whether the CEP database should be migrated on startup.
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = true;
}
