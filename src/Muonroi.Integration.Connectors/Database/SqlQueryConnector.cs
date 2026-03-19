using System.Data;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Connectors.Database;

/// <summary>
/// Parameterized SQL query connector (read-only by default).
/// Uses ADO.NET IDbConnection from DI.
/// </summary>
public sealed class SqlQueryConnector : IServiceTaskConnector
{
    private readonly IServiceProvider _serviceProvider;

    public SqlQueryConnector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ConnectorMetadata Metadata => new()
    {
        Type = "sql",
        DisplayName = "SQL Database Query",
        Category = "Database",
        IconSvg = "<path d=\"M12 3C7.58 3 4 4.79 4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7c0-2.21-3.58-4-8-4zm0 2c3.87 0 6 1.5 6 2s-2.13 2-6 2-6-1.5-6-2 2.13-2 6-2zM6 17V14.87c1.46.74 3.58 1.13 6 1.13s4.54-.39 6-1.13V17c0 .5-2.13 2-6 2s-6-1.5-6-2z\"/>",
        Description = "Execute parameterized SQL queries against a database connection. Read-only by default.",
        RequiresCredentials = true
    };

    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        JsonElement root = context.Config.RootElement;

        string query = root.GetProperty("query").GetString() ?? throw new InvalidOperationException("query is required");
        bool readOnly = !root.TryGetProperty("readOnly", out var ro) || ro.GetBoolean();

        // Safety check: block write operations if readOnly
        if (readOnly)
        {
            string upper = query.TrimStart().ToUpperInvariant();
            if (upper.StartsWith("INSERT") || upper.StartsWith("UPDATE") || upper.StartsWith("DELETE") || upper.StartsWith("DROP") || upper.StartsWith("ALTER") || upper.StartsWith("CREATE"))
            {
                return ConnectorResult.Fail("Write operations are blocked in read-only mode.");
            }
        }

        IDbConnection? connection = _serviceProvider.GetService<IDbConnection>();
        if (connection is null)
        {
            return ConnectorResult.Fail("No IDbConnection registered in DI.");
        }

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;

            // Bind parameters from FactBag
            if (root.TryGetProperty("parameters", out JsonElement parameters))
            {
                foreach (JsonProperty param in parameters.EnumerateObject())
                {
                    IDbDataParameter dbParam = cmd.CreateParameter();
                    dbParam.ParameterName = param.Name;
                    dbParam.Value = param.Value.ValueKind switch
                    {
                        JsonValueKind.String => param.Value.GetString(),
                        JsonValueKind.Number => param.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => DBNull.Value
                    };
                    cmd.Parameters.Add(dbParam);
                }
            }

            using IDataReader reader = cmd.ExecuteReader();
            List<Dictionary<string, object?>> rows = [];
            while (reader.Read())
            {
                Dictionary<string, object?> row = new();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            sw.Stop();
            return ConnectorResult.Ok(new()
            {
                ["sqlRows"] = rows,
                ["sqlRowCount"] = rows.Count
            }, duration: sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"SQL error: {ex.Message}", duration: sw.Elapsed);
        }
    }

    public Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        try
        {
            IDbConnection? connection = _serviceProvider.GetService<IDbConnection>();
            if (connection is null) return Task.FromResult(false);
            if (connection.State != ConnectionState.Open) connection.Open();
            return Task.FromResult(connection.State == ConnectionState.Open);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

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
}
