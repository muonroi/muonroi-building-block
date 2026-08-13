namespace Muonroi.RuleEngine.DecisionTable;

/// <summary>
/// Configuration options for the decision table engine and storage.
/// </summary>
public sealed class DecisionTableEngineOptions
{
    /// <summary>SQL Server connection string for persistence.</summary>
    public string? SqlServerConnectionString { get; set; }
    /// <summary>PostgreSQL connection string for persistence.</summary>
    public string? PostgresConnectionString { get; set; }
    /// <summary>Database schema name used for decision table tables.</summary>
    public string Schema { get; set; } = "dbo";
    /// <summary>Automatically migrates or creates the database on startup.</summary>
    public bool AutoMigrateDatabase { get; set; } = true;
}
