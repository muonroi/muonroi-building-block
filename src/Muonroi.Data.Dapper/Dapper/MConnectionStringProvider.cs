namespace Muonroi.Data.Dapper.Dapper;

/// <summary>
/// Provides connection strings from the application configuration.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MConnectionStringProvider"/> class.
/// </remarks>
/// <param name="configuration">The application configuration.</param>
public class MConnectionStringProvider(IConfiguration configuration)
    : IConnectionStringProvider
{
    /// <summary>
    /// Gets a connection string for the specified connection name.
    /// </summary>
    /// <param name="connectionName">The name of the connection.</param>
    /// <param name="enableMasterSlave">A value indicating whether to enable master-slave support.</param>
    /// <param name="readOnly">A value indicating whether to return a read-only connection string.</param>
    /// <returns>The connection string if found; otherwise, an empty string.</returns>
    public string GetConnectionString(string connectionName, bool enableMasterSlave = false, bool readOnly = false)
    {
        string configKey = $"{connectionName}:ConnectionString";
        return configuration.GetConnectionString(configKey) ?? configuration[configKey] ?? string.Empty;
    }
}
