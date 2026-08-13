using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Integration.Abstractions;
using System.Data;
using System.Text.Json;

namespace Muonroi.Integration.Connectors.Database;

/// <summary>
/// Parameterized SQL query connector (read-only by default).
/// Uses ADO.NET IDbConnection from DI.
/// </summary>
/// <remarks>
/// Creates a SQL connector using the provided service provider.
/// </remarks>
/// <param name="serviceProvider">Service provider for resolving <see cref="IDbConnection"/>.</param>
public sealed class SqlQueryConnector(IServiceProvider serviceProvider) : IServiceTaskConnector
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    /// Connector metadata describing capabilities and configuration.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "sql",
        DisplayName = "SQL Database Query",
        Category = "Database",
        IconSvg = "<path d=\"M12 3C7.58 3 4 4.79 4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7c0-2.21-3.58-4-8-4zm0 2c3.87 0 6 1.5 6 2s-2.13 2-6 2-6-1.5-6-2 2.13-2 6-2zM6 17V14.87c1.46.74 3.58 1.13 6 1.13s4.54-.39 6-1.13V17c0 .5-2.13 2-6 2s-6-1.5-6-2z\"/>",
        Description = "Execute parameterized SQL queries against a database connection. Read-only by default.",
        RequiresCredentials = true
    };

    /// <summary>
    /// Executes the configured SQL query.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            JsonElement root = context.Config.RootElement;
            string query = GetRequiredQuery(root);
            bool readOnly = IsReadOnly(root);

            if (IsBlockedWriteOperation(query, readOnly))
            {
                return Task.FromResult(
                    ConnectorResult.Fail("Write operations are blocked in read-only mode."));
            }

            IDbConnection? connection = _serviceProvider.GetService<IDbConnection>();
            if (connection is null)
            {
                return Task.FromResult(
                    ConnectorResult.Fail("No IDbConnection registered in DI."));
            }

            EnsureConnectionOpen(connection);

            using IDbCommand cmd = CreateCommand(connection, query, root);
            using IDataReader reader = cmd.ExecuteReader();

            List<Dictionary<string, object?>> rows = ReadRows(reader);

            sw.Stop();
            return Task.FromResult(
                ConnectorResult.Ok(
                    new()
                    {
                        ["sqlRows"] = rows,
                        ["sqlRowCount"] = rows.Count
                    },
                    duration: sw.Elapsed));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(
                ConnectorResult.Fail($"SQL error: {ex.Message}", duration: sw.Elapsed));
        }
    }

    /// <summary>
    /// Tests whether the database connection can be opened.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the connection succeeds.</returns>
    public Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        try
        {
            IDbConnection? connection = _serviceProvider.GetService<IDbConnection>();
            if (connection is null)
            {
                return Task.FromResult(false);
            }

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            return Task.FromResult(connection.State == ConnectionState.Open);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Returns the JSON schema used to configure this connector.
    /// </summary>
    /// <returns>Configuration schema.</returns>
    public JsonElement GetConfigSchema()
    {
        string schema = """
        {
            "type": "object",
            "properties": {
                "query": { "type": "string", "description": "SQL query (use @param for parameters)" },
                "parameters": { "type": "object", "additionalProperties": true, "description": "Query parameters" },
                "readOnly": { "type": "boolean", "default": true, "description": "Block write operations" }
            },
            "required": ["query"]
        }
        """;
        return JsonDocument.Parse(schema).RootElement.Clone();
    }
    private static string GetRequiredQuery(JsonElement root)
    {
        string? query = root.GetProperty("query").GetString();
        return MGuard.NotNull(query);
    }

    private static bool IsReadOnly(JsonElement root)
    {
        return !root.TryGetProperty("readOnly", out JsonElement ro) || ro.GetBoolean();
    }

    private static bool IsBlockedWriteOperation(string query, bool readOnly)
    {
        if (!readOnly)
        {
            return false;
        }

        string upper = query.TrimStart().ToUpperInvariant();

        return upper.StartsWith("INSERT")
            || upper.StartsWith("UPDATE")
            || upper.StartsWith("DELETE")
            || upper.StartsWith("DROP")
            || upper.StartsWith("ALTER")
            || upper.StartsWith("CREATE");
    }

    private static void EnsureConnectionOpen(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    private static IDbCommand CreateCommand(
        IDbConnection connection,
        string query,
        JsonElement root)
    {
        IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.CommandType = CommandType.Text;

        BindParameters(cmd, root);
        return cmd;
    }

    private static void BindParameters(IDbCommand cmd, JsonElement root)
    {
        if (!root.TryGetProperty("parameters", out JsonElement parameters))
        {
            return;
        }

        foreach (JsonProperty param in parameters.EnumerateObject())
        {
            IDbDataParameter dbParam = cmd.CreateParameter();
            dbParam.ParameterName = param.Name;
            dbParam.Value = ConvertJsonValue(param.Value);
            cmd.Parameters.Add(dbParam);
        }
    }

    private static object ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? (object)DBNull.Value,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => DBNull.Value
        };
    }

    private static List<Dictionary<string, object?>> ReadRows(IDataReader reader)
    {
        List<Dictionary<string, object?>> rows = [];

        while (reader.Read())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static Dictionary<string, object?> ReadRow(IDataReader reader)
    {
        Dictionary<string, object?> row = [];

        for (int i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }
}
