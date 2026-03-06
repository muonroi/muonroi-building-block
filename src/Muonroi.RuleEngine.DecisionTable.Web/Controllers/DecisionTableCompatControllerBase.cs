using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Serializers;
using Muonroi.RuleEngine.DecisionTable.Stores;
using Muonroi.RuleEngine.DecisionTable.Validators;
using Muonroi.RuleEngine.DecisionTable.Web.ViewModels;
using DecisionTableValidationResult = Muonroi.RuleEngine.DecisionTable.Validators.ValidationResult;

namespace Muonroi.RuleEngine.DecisionTable.Web.Controllers;

public abstract class DecisionTableCompatControllerBase(
    IDecisionTableStore store,
    DecisionTableValidator validator) : ControllerBase
{
    [HttpGet]
    public virtual async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        DecisionTableQuery query = new()
        {
            Page = page,
            PageSize = pageSize,
            Search = search
        };

        DecisionTablePageResult result = await store.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public virtual async Task<IActionResult> Get(string id, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        return table is null ? NotFound() : Ok(table);
    }

    [HttpPost]
    public virtual async Task<IActionResult> Create(
        [FromBody] DecisionTableModel table,
        CancellationToken cancellationToken = default)
    {
        DecisionTableValidationResult validation = validator.Validate(table);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        table.Id = Guid.NewGuid().ToString("N");
        table.Version = Math.Max(1, table.Version);
        table.CreatedAt = DateTimeOffset.UtcNow;
        table.ModifiedAt = DateTimeOffset.UtcNow;

        await store.SaveAsync(table, ResolveActor(), ResolveCreateReason(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = table.Id }, table);
    }

    [HttpPut("{id}")]
    public virtual async Task<IActionResult> Update(
        string id,
        [FromBody] DecisionTableModel table,
        CancellationToken cancellationToken = default)
    {
        DecisionTableModel? existing = await store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        DecisionTableValidationResult validation = validator.Validate(table);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        table.Id = id;
        table.CreatedAt = existing.CreatedAt;
        await store.SaveAsync(table, ResolveActor(), ResolveUpdateReason(), cancellationToken);

        DecisionTableModel? saved = await store.GetByIdAsync(id, cancellationToken);
        return Ok(saved ?? table);
    }

    [HttpPost("{id}/rows/reorder")]
    public virtual async Task<IActionResult> ReorderRows(
        string id,
        [FromBody] DecisionTableRowReorderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RowIds.Count == 0)
        {
            return BadRequest(new { message = "RowIds must not be empty." });
        }

        bool ok = await store.ReorderRowsAsync(
            id,
            request.RowIds,
            ResolveActor(request.Actor),
            request.Reason ?? ResolveReorderReason(),
            cancellationToken);

        if (!ok)
        {
            return NotFound(new { message = "Decision table not found or invalid rowIds." });
        }

        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        return Ok(table);
    }

    [HttpGet("{id}/versions")]
    public virtual async Task<IActionResult> Versions(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DecisionTableVersionSnapshot> versions = await store.GetVersionHistoryAsync(id, page, pageSize, cancellationToken);
        return Ok(versions);
    }

    [HttpPost("{id}/validate")]
    public virtual async Task<IActionResult> Validate(string id, CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound(new { message = $"Decision table '{id}' not found." });
        }

        DecisionTableValidationResult result = validator.Validate(table);
        return Ok(result);
    }

    [HttpGet("{id}/export/{format}")]
    public virtual async Task<IActionResult> Export(
        string id,
        string format,
        CancellationToken cancellationToken = default)
    {
        DecisionTableModel? table = await store.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound(new { message = $"Decision table '{id}' not found." });
        }

        string normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "json" => File(
                Encoding.UTF8.GetBytes(DecisionTableJsonSerializer.Serialize(table)),
                "application/json",
                $"{table.Name}.json"),
            "dmn" or "xml" => File(
                Encoding.UTF8.GetBytes(DecisionTableXmlSerializer.SerializeToDmnXml(table)),
                "application/xml",
                $"{table.Name}.dmn.xml"),
            "excel" or "xlsx" => File(
                Encoding.UTF8.GetBytes(SerializeAsCsv(table)),
                "text/csv",
                $"{table.Name}.csv"),
            _ => BadRequest(new { message = "Unsupported export format. Use json|dmn|excel." })
        };
    }

    protected virtual string? ResolveActor(string? actorFromRequest = null)
    {
        return string.IsNullOrWhiteSpace(actorFromRequest) ? ResolveActor() : actorFromRequest;
    }

    protected virtual string ResolveActor()
    {
        MControllerExecutionContext? context = ResolveExecutionContext();
        if (!string.IsNullOrWhiteSpace(context?.Actor))
        {
            return context.Actor;
        }

        if (!string.IsNullOrWhiteSpace(context?.Username))
        {
            return context.Username;
        }

        return "anonymous";
    }

    protected virtual string ResolveCreateReason() => "create";

    protected virtual string ResolveUpdateReason() => "update";

    protected virtual string ResolveReorderReason() => "reorder";

    protected virtual MControllerExecutionContext? ResolveExecutionContext()
    {
        HttpContext? httpContext = HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        IMControllerExecutionContextResolver? resolver =
            httpContext.RequestServices.GetService(typeof(IMControllerExecutionContextResolver))
                as IMControllerExecutionContextResolver;

        return resolver?.Resolve(httpContext) ?? BuildFallbackExecutionContext(httpContext);
    }

    private static MControllerExecutionContext BuildFallbackExecutionContext(HttpContext httpContext)
    {
        IAuthenticateInfoContext? authContext =
            httpContext.RequestServices.GetService(typeof(IAuthenticateInfoContext)) as IAuthenticateInfoContext;

        ClaimsPrincipal user = httpContext.User;
        string? username = authContext?.CurrentUsername
                           ?? user.FindFirst(ClaimConstants.Username)?.Value
                           ?? user.Identity?.Name
                           ?? ReadHeader(httpContext, "X-Username");

        string? actor = ReadHeader(httpContext, "X-Actor") ?? username;
        string? tenantId = authContext?.TenantId
                           ?? user.FindFirst(ClaimConstants.TenantId)?.Value
                           ?? ReadHeader(httpContext, "X-Tenant-Id")
                           ?? ReadHeader(httpContext, "TenantId");

        Guid? userId = ParseGuid(
            authContext?.CurrentUserGuid
            ?? user.FindFirst(ClaimConstants.UserIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ReadHeader(httpContext, "X-User-Id"));

        IReadOnlyList<string> permissions = ParsePermissions(
            authContext?.Permission
            ?? user.FindFirst(ClaimConstants.Permission)?.Value
            ?? ReadHeader(httpContext, "X-Permissions"));

        return new MControllerExecutionContext
        {
            UserId = userId,
            Username = username,
            TenantId = tenantId,
            Actor = actor,
            IsAuthenticated = authContext?.IsAuthenticated == true || user.Identity?.IsAuthenticated == true,
            Permissions = permissions
        };
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ParsePermissions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadHeader(HttpContext httpContext, string key)
    {
        if (!httpContext.Request.Headers.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues values))
        {
            return null;
        }

        string? value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SerializeAsCsv(DecisionTableModel table)
    {
        List<string> headers = [];
        headers.AddRange(table.InputColumns.Select(x => $"IN:{x.Name}"));
        headers.AddRange(table.OutputColumns.Select(x => $"OUT:{x.Name}"));

        StringBuilder builder = new();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (DecisionTableRow row in table.Rows.OrderBy(x => x.Order))
        {
            List<string> values = [];
            values.AddRange(row.InputCells.Select(x => x.Expression));
            values.AddRange(row.OutputCells.Select(x => x.Expression));
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        string raw = value ?? string.Empty;
        if (raw.Contains(',', StringComparison.Ordinal) ||
            raw.Contains('"', StringComparison.Ordinal) ||
            raw.Contains('\n', StringComparison.Ordinal) ||
            raw.Contains('\r', StringComparison.Ordinal))
        {
            return "\"" + raw.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return raw;
    }
}
