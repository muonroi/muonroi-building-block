namespace Muonroi.Core.Abstractions.Models.Common;

public class DatabaseConfigs
{
    public string DbType { get; set; } = string.Empty;
    public ConnectionStrings? ConnectionStrings { get; set; }
    public DatabaseSettings? DatabaseSettings { get; set; }
}

public class ConnectionStrings
{
    public string? MongoDbConnectionString { get; set; }
    public string? SqlServerConnectionString { get; set; }
    public string? MySqlConnectionString { get; set; }
    public string? PostgreSqlConnectionString { get; set; }
    public string? SqliteConnectionString { get; set; }
}

public class DatabaseSettings
{
    public string DatabaseName { get; set; } = string.Empty;
}
