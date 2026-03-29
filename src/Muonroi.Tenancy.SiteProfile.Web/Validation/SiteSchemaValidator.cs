using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Web.Validation;

/// <summary>
/// IHostedService that validates EF Core model columns against the actual database schema
/// at startup, before serving the first request.
///
/// Compares EF Core <see cref="IRelationalModel"/> metadata (columns declared in the model)
/// against <c>INFORMATION_SCHEMA.COLUMNS</c> (columns actually in the database).
/// Checks: column existence and IS_NULLABLE. Does NOT check data type (per D-17).
///
/// Separate from <c>SiteProfileStartupValidator</c> which validates DI keyed service registrations (per D-16).
///
/// Usage:
/// <code>
/// // In Program.cs (opt-in — does nothing if not called):
/// services.AddSiteSchemaValidation(o =>
/// {
///     o.Severity = SchemaValidationSeverity.FailOnMissing;  // strict mode
///     o.SchemaName = "public";                               // PostgreSQL default
/// });
/// </code>
/// </summary>
public sealed class SiteSchemaValidator : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly SiteSchemaValidationOptions _options;
    private readonly IMLog<SiteSchemaValidator> _log;

    /// <summary>
    /// Initializes a new instance of <see cref="SiteSchemaValidator"/>.
    /// </summary>
    /// <param name="sp">The root service provider (used to create a scope for DbContext resolution).</param>
    /// <param name="options">Validation options controlling severity and schema name.</param>
    /// <param name="log">Logger for reporting mismatches.</param>
    public SiteSchemaValidator(
        IServiceProvider sp,
        IOptions<SiteSchemaValidationOptions> options,
        IMLog<SiteSchemaValidator> log)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(log);

        _sp = sp;
        _options = options.Value;
        _log = log;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Discover all registered DbContext instances available from the scope
        var contexts = sp.GetServices<DbContext>().ToList();

        if (contexts.Count == 0)
        {
            _log.Info("[SiteSchemaValidator] No DbContext instances found in DI — skipping schema validation.");
            return;
        }

        var allMismatches = new List<string>();

        foreach (DbContext context in contexts)
        {
            List<string> mismatches = await ValidateContextAsync(context, ct);
            allMismatches.AddRange(mismatches);
        }

        if (allMismatches.Count == 0)
        {
            _log.Info("[SiteSchemaValidator] Schema validation passed — all EF model columns match the database.");
            return;
        }

        if (_options.Severity == SchemaValidationSeverity.FailOnMissing)
        {
            string message =
                $"[SiteSchemaValidator] Schema validation FAILED — {allMismatches.Count} mismatch(es) found:\n" +
                string.Join("\n", allMismatches);
            throw new InvalidOperationException(message);
        }
        else
        {
            // WarnOnMismatch — log each mismatch but do not throw
            foreach (string mismatch in allMismatches)
            {
                _log.Warn("[SiteSchemaValidator] Schema mismatch: {Mismatch}", mismatch);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<List<string>> ValidateContextAsync(DbContext context, CancellationToken ct)
    {
        var mismatches = new List<string>();

        // Get EF relational model metadata
        IRelationalModel? relationalModel = context.Model.GetRelationalModel();
        if (relationalModel is null)
        {
            _log.Info(
                "[SiteSchemaValidator] Context '{ContextType}' has no relational model — skipping.",
                context.GetType().Name);
            return mismatches;
        }

        // Get ADO.NET connection (raw — no EF overhead, no Dapper process-global state)
        DbConnection connection = context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync(ct);

            foreach (ITable table in relationalModel.Tables)
            {
                // Query INFORMATION_SCHEMA.COLUMNS for this table
                Dictionary<string, bool> actualColumns = await QueryActualColumnsAsync(
                    connection, table.Name, _options.SchemaName, ct);

                // Compare EF model columns vs actual
                foreach (IColumn efColumn in table.Columns)
                {
                    if (!actualColumns.TryGetValue(efColumn.Name, out bool actualIsNullable))
                    {
                        mismatches.Add(
                            $"  - Table '{_options.SchemaName}.{table.Name}': column '{efColumn.Name}' " +
                            $"exists in EF model but NOT in database.");
                    }
                    else
                    {
                        // Check nullable mismatch (column existence + nullable per D-17, no data type)
                        bool efIsNullable = efColumn.IsNullable;
                        if (efIsNullable != actualIsNullable)
                        {
                            mismatches.Add(
                                $"  - Table '{_options.SchemaName}.{table.Name}': column '{efColumn.Name}' " +
                                $"nullable mismatch — EF model: {efIsNullable}, database: {actualIsNullable}.");
                        }
                    }
                }
            }
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }

        return mismatches;
    }

    /// <summary>
    /// Queries INFORMATION_SCHEMA.COLUMNS for all columns in a given table.
    /// Returns a dictionary of column name → is_nullable (true = YES, false = NO).
    /// Uses raw ADO.NET — no EF overhead, no Dapper process-global SqlMapper.
    /// </summary>
    private static async Task<Dictionary<string, bool>> QueryActualColumnsAsync(
        DbConnection connection,
        string tableName,
        string schemaName,
        CancellationToken ct)
    {
        var columns = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COLUMN_NAME, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
            """;

        DbParameter schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@Schema";
        schemaParam.Value = schemaName;
        cmd.Parameters.Add(schemaParam);

        DbParameter tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@TableName";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);

        using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string columnName = reader.GetString(0);
            string isNullableStr = reader.GetString(1); // "YES" or "NO"
            bool isNullable = string.Equals(isNullableStr, "YES", StringComparison.OrdinalIgnoreCase);
            columns[columnName] = isNullable;
        }

        return columns;
    }
}
