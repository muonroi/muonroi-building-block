using System.Text.Json;
using System.Xml.Linq;

namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

/// <summary>
/// API endpoints for managing decision tables.
/// </summary>
[ApiController]
[Route("api/v1/decision-tables")]
[Route("api/v1/rule-engine/decision-tables")]
public sealed class DecisionTableController(
    IDecisionTableStore store,
    DecisionTableValidator validator,
    IDecisionTableExecutor executor) : ControllerBase
{
    /// <summary>
    /// Lists decision tables with filters and pagination.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="tenantId">Optional tenant id filter.</param>
    /// <param name="hitPolicy">Optional hit policy filter.</param>
    /// <param name="includeDeleted">Include soft-deleted tables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decision table page.</returns>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? hitPolicy = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        DecisionTableQuery query = new()
        {
            Page = page,
            PageSize = pageSize,
            Search = search,
            TenantId = tenantId,
            IncludeDeleted = includeDeleted,
            HitPolicy = ParseHitPolicyOrNull(hitPolicy)
        };

        DecisionTablePageResult result = await store.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a decision table by identifier.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decision table.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        return table is null ? NotFound() : Ok(table);
    }

    /// <summary>
    /// Creates a decision table.
    /// </summary>
    /// <param name="table">Decision table payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created decision table.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DecisionTableModel table, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = validator.Validate(table);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        table.Id = Guid.NewGuid().ToString("N");
        table.Version = Math.Max(1, table.Version);
        table.CreatedAt = DateTimeOffset.UtcNow;
        table.ModifiedAt = DateTimeOffset.UtcNow;

        await store.SaveAsync(table, ResolveActor(), "create", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = table.Id }, table);
    }

    /// <summary>
    /// Updates an existing decision table.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="table">Decision table payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated decision table.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] DecisionTableModel table, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        ValidationResult validation = validator.Validate(table);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        table.Id = id;
        table.CreatedAt = existing.CreatedAt;

        await store.SaveAsync(table, ResolveActor(), "update", cancellationToken);
        DecisionTableModel? persisted = await store.GetByIdAsync(id, cancellationToken);
        return Ok(persisted ?? table);
    }

    /// <summary>
    /// Bulk creates or updates decision tables.
    /// </summary>
    /// <param name="request">Bulk upsert request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bulk upsert result.</returns>
    [HttpPost("bulk/upsert")]
    public async Task<IActionResult> BulkUpsert(
        [FromBody] DecisionTableBulkUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Tables.Count == 0)
        {
            return BadRequest(new { message = "Tables must not be empty." });
        }

        foreach (DecisionTableModel table in request.Tables)
        {
            ValidationResult validation = validator.Validate(table);
            if (!validation.IsValid)
            {
                return BadRequest(new { tableId = table.Id, validation });
            }

            if (string.IsNullOrWhiteSpace(table.Id))
            {
                table.Id = Guid.NewGuid().ToString("N");
            }
        }

        DecisionTableBulkResult result = await store.BulkUpsertAsync(request.Tables, ResolveActor(request.Actor), request.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Bulk deletes decision tables by id.
    /// </summary>
    /// <param name="request">Bulk delete request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bulk delete result.</returns>
    [HttpPost("bulk/delete")]
    public async Task<IActionResult> BulkDelete(
        [FromBody] DecisionTableBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest(new { message = "Ids must not be empty." });
        }

        DecisionTableBulkResult result = await store.BulkDeleteAsync(request.Ids, ResolveActor(request.Actor), request.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Imports a decision table from a file.
    /// </summary>
    /// <param name="file">Input file.</param>
    /// <param name="format">Optional format override.</param>
    /// <param name="tenantId">Optional tenant id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported decision table.</returns>
    [HttpPost("import")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile file,
        [FromForm] string? format = null,
        [FromForm] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Import file is required." });
        }

        string normalizedFormat = NormalizeImportFormat(format, file.FileName);
        DecisionTableModel table;

        await using Stream stream = file.OpenReadStream();
        switch (normalizedFormat)
        {
            case "excel":
                table = ExcelToDecisionTableConverter.Convert(stream, Path.GetFileNameWithoutExtension(file.FileName), tenantId);
                break;
            case "json":
            {
                table = await JsonSerializer.DeserializeAsync<DecisionTableModel>(stream, cancellationToken: cancellationToken) // MBB002-exempt: stream-based async overload not available in IMJsonSerializeService wrapper
                    ?? new DecisionTableModel();
                break;
            }
            case "dmn":
                table = await ParseDmnAsync(stream, tenantId, cancellationToken);
                break;
            default:
                return BadRequest(new { message = "Unsupported import format. Use excel|json|dmn." });
        }

        ValidationResult validation = validator.Validate(table);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        table.Id = string.IsNullOrWhiteSpace(table.Id) ? Guid.NewGuid().ToString("N") : table.Id;
        table.CreatedAt = DateTimeOffset.UtcNow;
        table.ModifiedAt = DateTimeOffset.UtcNow;

        await store.SaveAsync(table, ResolveActor(), $"import:{normalizedFormat}", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = table.Id }, table);
    }

    /// <summary>
    /// Reorders rows for a decision table.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="request">Reorder request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated decision table.</returns>
    [HttpPost("{id}/rows/reorder")]
    public async Task<IActionResult> ReorderRows(
        string id,
        [FromBody] DecisionTableRowReorderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RowIds.Count == 0)
        {
            return BadRequest(new { message = "RowIds must not be empty." });
        }

        bool reordered = await store.ReorderRowsAsync(
            id,
            request.RowIds,
            ResolveActor(request.Actor),
            request.Reason,
            cancellationToken);

        if (!reordered)
        {
            return NotFound(new { message = "Decision table not found or row set mismatch." });
        }

        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        return Ok(table);
    }

    /// <summary>
    /// Executes a decision table with provided inputs.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="request">Execution request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    [HttpPost("{id}/execute")]
    public async Task<IActionResult> Execute(
        string id,
        [FromBody] DecisionTableExecuteRequest? request,
        CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        IReadOnlyDictionary<string, object?> inputs = (request?.Inputs ?? new Dictionary<string, object?>())
            .ToDictionary(
                x => x.Key,
                x => NormalizeInputValue(x.Value),
                StringComparer.OrdinalIgnoreCase);

        DecisionTableExecutionResult executionResult;
        try
        {
            executionResult = await executor.ExecuteAsync(table, inputs, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return Ok(new DecisionTableExecuteResponse
        {
            Matched = executionResult.Matched,
            HitPolicy = executionResult.HitPolicy.ToString(),
            EvaluationTimeMs = executionResult.EvaluationTime.TotalMilliseconds,
            MatchedRowIds = executionResult.MatchedRowIds,
            Outputs = [.. executionResult.Outputs.Select(x => new DecisionTableOutputItem
            {
                RowId = x.RowId,
                Outputs = x.Outputs
            })]
        });
    }

    /// <summary>
    /// Returns version history for a decision table.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version history.</returns>
    [HttpGet("{id}/versions")]
    public async Task<IActionResult> GetVersionHistory(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DecisionTableVersionSnapshot> versions = await store.GetVersionHistoryAsync(id, page, pageSize, cancellationToken);
        return Ok(versions);
    }

    /// <summary>
    /// Gets a decision table version snapshot.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="version">Version number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version snapshot.</returns>
    [HttpGet("{id}/versions/{version:int}")]
    public async Task<IActionResult> GetVersion(string id, int version, CancellationToken cancellationToken = default)
    {
        DecisionTableVersionSnapshot? snapshot = await store.GetVersionAsync(id, version, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    /// <summary>
    /// Returns audit trail entries for a decision table.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit trail entries.</returns>
    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAuditTrail(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DecisionTableAuditEntry> entries = await store.GetAuditTrailAsync(id, page, pageSize, cancellationToken);
        return Ok(entries);
    }

    /// <summary>
    /// Returns a global audit trail across decision tables.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit trail entries.</returns>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditTrailGlobal(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DecisionTableAuditEntry> entries = await store.GetAuditTrailAsync(page: page, pageSize: pageSize, cancellationToken: cancellationToken);
        return Ok(entries);
    }

    /// <summary>
    /// Deletes a decision table by id.
    /// </summary>
    /// <param name="id">Decision table identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        await store.BulkDeleteAsync([id], ResolveActor(), "single-delete", cancellationToken);
        return NoContent();
    }

    private string? ResolveActor(string? actorFromRequest = null)
    {
        if (!string.IsNullOrWhiteSpace(actorFromRequest))
        {
            return actorFromRequest;
        }

        if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
        {
            return User.Identity.Name;
        }

        if (Request.Headers.TryGetValue("X-Actor", out Microsoft.Extensions.Primitives.StringValues actorHeader))
        {
            string? actor = actorHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(actor))
            {
                return actor;
            }
        }

        return null;
    }

    private static HitPolicy? ParseHitPolicyOrNull(string? hitPolicy)
    {
        if (string.IsNullOrWhiteSpace(hitPolicy))
        {
            return null;
        }

        string normalized = hitPolicy.Replace("_", string.Empty, StringComparison.Ordinal);
        foreach (HitPolicy value in Enum.GetValues<HitPolicy>())
        {
            if (string.Equals(value.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeImportFormat(string? format, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            return format.Trim().ToLowerInvariant() switch
            {
                "xlsx" => "excel",
                "xls" => "excel",
                "excel" => "excel",
                "json" => "json",
                "xml" => "dmn",
                "dmn" => "dmn",
                var x => x
            };
        }

        string extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "xlsx" or "xls" => "excel",
            "json" => "json",
            "xml" or "dmn" => "dmn",
            _ => string.Empty
        };
    }

    private static async Task<DecisionTableModel> ParseDmnAsync(
        Stream stream,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        XElement? decision = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "decision");
        XElement? table = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "decisionTable");

        DecisionTableModel result = new()
        {
            Id = decision?.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N"),
            Name = decision?.Attribute("name")?.Value ?? "Imported DMN",
            Description = decision?.Elements().FirstOrDefault(x => x.Name.LocalName == "description")?.Value ?? string.Empty,
            TenantId = tenantId
        };

        string? hitPolicy = table?.Attribute("hitPolicy")?.Value;
        if (!string.IsNullOrWhiteSpace(hitPolicy) && Enum.TryParse(hitPolicy, true, out HitPolicy parsedPolicy))
        {
            result.HitPolicy = parsedPolicy;
        }

        List<string> inputIds = [];
        foreach (XElement inputExpression in table?.Descendants().Where(x => x.Name.LocalName == "inputExpression") ?? [])
        {
            string id = inputExpression.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N");
            string name = inputExpression.Descendants().FirstOrDefault(x => x.Name.LocalName == "text")?.Value ?? id;
            result.InputColumns.Add(new DecisionTableColumn
            {
                Id = id,
                Name = name,
                Label = name,
                DataType = "string"
            });
            inputIds.Add(id);
        }

        List<string> outputIds = [];
        foreach (XElement outputClause in table?.Descendants().Where(x => x.Name.LocalName == "outputClause") ?? [])
        {
            string id = outputClause.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N");
            string name = outputClause.Attribute("name")?.Value ?? id;
            string label = outputClause.Attribute("label")?.Value ?? name;
            result.OutputColumns.Add(new DecisionTableColumn
            {
                Id = id,
                Name = name,
                Label = label,
                DataType = "string"
            });
            outputIds.Add(id);
        }

        int rowOrder = 0;
        foreach (XElement rule in table?.Descendants().Where(x => x.Name.LocalName == "rule") ?? [])
        {
            DecisionTableRow row = new()
            {
                Id = rule.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N"),
                Order = rowOrder++
            };

            List<string> inputValues = [.. rule.Elements().Where(x => x.Name.LocalName == "inputEntry")
                .Select(x => x.Descendants().FirstOrDefault(t => t.Name.LocalName == "text")?.Value ?? "-")];
            for (int i = 0; i < result.InputColumns.Count; i++)
            {
                row.InputCells.Add(new DecisionTableCell
                {
                    ColumnId = inputIds.ElementAtOrDefault(i) ?? result.InputColumns[i].Id,
                    Expression = inputValues.ElementAtOrDefault(i) ?? "-"
                });
            }

            List<string> outputValues = [.. rule.Elements().Where(x => x.Name.LocalName == "outputEntry")
                .Select(x => x.Descendants().FirstOrDefault(t => t.Name.LocalName == "text")?.Value ?? string.Empty)];
            for (int i = 0; i < result.OutputColumns.Count; i++)
            {
                row.OutputCells.Add(new DecisionTableCell
                {
                    ColumnId = outputIds.ElementAtOrDefault(i) ?? result.OutputColumns[i].Id,
                    Expression = outputValues.ElementAtOrDefault(i) ?? string.Empty
                });
            }

            result.Rows.Add(row);
        }

        return result;
    }

    private static object? NormalizeInputValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out int intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out double doubleValue) => doubleValue,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(x => NormalizeInputValue(x)).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(x => x.Name, x => NormalizeInputValue(x.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }
}
