using Dapper.Extensions;

namespace Muonroi.Data.Dapper.PostgreSql.IntegrationTests.Fixtures;

/// <summary>
/// A minimal <see cref="IConnectionStringProvider"/> that always returns a fixed connection
/// string for the <c>"default"</c> key. Used by <c>RlsStartupVerifier</c> in integration tests
/// to supply the container's connection string without a full DI container build.
/// </summary>
internal sealed class FixedConnectionStringProvider : IConnectionStringProvider
{
    private readonly string _connectionString;

    public FixedConnectionStringProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string GetConnectionString(string connectionName, bool enableMasterSlave = false, bool readOnly = false)
        => _connectionString;
}
