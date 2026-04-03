namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents the database configuration settings. </summary>
public class DatabaseConfigs
{
    /// <summary> Gets or sets the type of the database. </summary>
    public string DbType { get; set; } = string.Empty;

    /// <summary> Gets or sets the connection strings for different database types. </summary>
    public ConnectionStrings? ConnectionStrings { get; set; }

    /// <summary> Gets or sets the general database settings. </summary>
    public DatabaseSettings? DatabaseSettings { get; set; }
}

/// <summary> Represents the connection strings for various database systems. </summary>
public class ConnectionStrings
{
    /// <summary> Gets or sets the MongoDB connection string. </summary>
    public string? MongoDbConnectionString { get; set; }

    /// <summary> Gets or sets the SQL Server connection string. </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary> Gets or sets the MySQL connection string. </summary>
    public string? MySqlConnectionString { get; set; }

    /// <summary> Gets or sets the PostgreSQL connection string. </summary>
    public string? PostgreSqlConnectionString { get; set; }

    /// <summary> Gets or sets the SQLite connection string. </summary>
    public string? SqliteConnectionString { get; set; }
}

/// <summary> Represents specific database settings. </summary>
public class DatabaseSettings
{
    /// <summary> Gets or sets the name of the database. </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
