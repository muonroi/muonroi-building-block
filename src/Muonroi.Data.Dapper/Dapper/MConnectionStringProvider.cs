namespace Muonroi.Data.Dapper.Dapper;

public class MConnectionStringProvider(IConfiguration configuration)
    : IConnectionStringProvider
{
    public string GetConnectionString(string connectionName, bool enableMasterSlave = false, bool readOnly = false)
    {
        string configKey = $"{connectionName}:ConnectionString";
        return configuration.GetConnectionString(configKey) ?? configuration[configKey] ?? string.Empty;
    }
}
