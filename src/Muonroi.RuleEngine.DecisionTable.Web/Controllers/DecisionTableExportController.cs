namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

/// <summary>
/// API endpoints for exporting decision tables.
/// </summary>
[ApiController]
[Route("api/v1/decision-tables/{id}/export")]
[Route("api/v1/rule-engine/decision-tables/{id}/export")]
public sealed class DecisionTableExportController(
    IDecisionTableStore store,
    IDecisionTableConverter converter) : ControllerBase
{
    /// <summary>
    /// Exports a decision table to the requested format.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="format">Export format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported file payload.</returns>
    [HttpGet("{format}")]
    public async Task<IActionResult> Export(string id, string format, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        string normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "json" => File(
                Encoding.UTF8.GetBytes(converter.Convert(table)),
                "application/json",
                $"{table.Name}.json"),
            "xml" => File(
                Encoding.UTF8.GetBytes(DecisionTableXmlSerializer.SerializeToDmnXml(table)),
                "application/xml",
                $"{table.Name}.xml"),
            "dmn" => File(
                Encoding.UTF8.GetBytes(DecisionTableXmlSerializer.SerializeToDmnXml(table)),
                "application/xml",
                $"{table.Name}.dmn.xml"),
            "excel" => File(
                Encoding.UTF8.GetBytes(BuildCsv(table)),
                "text/csv",
                $"{table.Name}.csv"),
            _ => BadRequest(new { message = "Unsupported export format. Use excel|json|xml|dmn." })
        };
    }

    /// <summary>
    /// Exports a decision table as JSON.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported JSON file.</returns>
    [HttpPost("json")]
    public async Task<IActionResult> ExportJson(string id, CancellationToken cancellationToken = default)
    {
        return await Export(id, "json", cancellationToken);
    }

    /// <summary>
    /// Exports a decision table as DMN XML.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported DMN file.</returns>
    [HttpPost("dmn")]
    public async Task<IActionResult> ExportDmn(string id, CancellationToken cancellationToken = default)
    {
        return await Export(id, "dmn", cancellationToken);
    }

    private static string BuildCsv(DecisionTableModel table)
    {
        StringBuilder builder = new();
        List<string> headers = [.. table.InputColumns.Select(x => $"in:{x.Name}"), .. table.OutputColumns.Select(x => $"out:{x.Name}")];
        builder.AppendLine(string.Join(',', headers));

        foreach (DecisionTableRow row in table.Rows.OrderBy(x => x.Order))
        {
            List<string> cells = [.. row.InputCells.Select(x => EscapeCsv(x.Expression)), .. row.OutputCells.Select(x => EscapeCsv(x.Expression))];
            builder.AppendLine(string.Join(',', cells));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
