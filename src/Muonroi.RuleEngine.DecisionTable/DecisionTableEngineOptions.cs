namespace Muonroi.RuleEngine.DecisionTable;

public sealed class DecisionTableEngineOptions
{
    public string? SqlServerConnectionString { get; set; }
    public string? PostgresConnectionString { get; set; }
    public string Schema { get; set; } = "dbo";
    public bool AutoMigrateDatabase { get; set; } = true;
}
