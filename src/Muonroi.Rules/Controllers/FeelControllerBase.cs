using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Rules.Feel;

namespace Muonroi.Rules.Controllers;

public abstract class FeelControllerBase : ControllerBase
{
    [HttpPost("evaluate")]
    public virtual IActionResult Evaluate([FromBody] FeelEvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return BadRequest(new { message = "Expression is required." });
        }

        Dictionary<string, object> variables = request.Context.ToDictionary(
            x => x.Key,
            x => ConvertJsonValue(x.Value),
            StringComparer.OrdinalIgnoreCase);

        object? result = FeelParser.Parse(request.Expression, variables);
        return Ok(new
        {
            success = result is not null,
            result,
            expression = request.Expression
        });
    }

    [HttpPost("autocomplete")]
    public virtual IActionResult Autocomplete([FromBody] FeelAutocompleteRequest request)
    {
        string partial = request.PartialExpression?.Trim() ?? string.Empty;
        string token = ExtractLastToken(partial);

        IEnumerable<string> candidates = GetKeywords()
            .Concat(request.Context?.Keys ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        List<string> suggestions = [.. candidates
            .Where(x => x.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(25)];

        return Ok(new
        {
            partialExpression = partial,
            token,
            dataType = request.DataType,
            suggestions
        });
    }

    [HttpGet("examples")]
    public virtual IActionResult Examples()
    {
        return Ok(new
        {
            numeric = GetNumericExamples(),
            stringOps = GetStringExamples(),
            listAndContext = GetListExamples()
        });
    }

    protected virtual IReadOnlyList<string> GetKeywords() =>
    [
        "if",
        "then",
        "else",
        "for",
        "in",
        "some",
        "every",
        "satisfies",
        "instance of",
        "between",
        "and",
        "or",
        "not"
    ];

    protected virtual IReadOnlyList<string> GetNumericExamples() =>
    [
        "amount > 1000",
        "score in [80..100]",
        "if score >= 90 then \"A\" else \"B\""
    ];

    protected virtual IReadOnlyList<string> GetStringExamples() =>
    [
        "customerType in (\"vip\", \"gold\")",
        "country matches \"^(US|CA)$\""
    ];

    protected virtual IReadOnlyList<string> GetListExamples() =>
    [
        "for x in [1..5] return x * 2",
        "some item in items satisfies item.price > 100"
    ];

    protected static string ExtractLastToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts = value.Split([' ', '\t', '\n', '\r', '(', ')', '[', ']', '{', '}', ','],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    protected static object ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(x => x.Name, x => ConvertJsonValue(x.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => string.Empty
        };
    }

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

        string? tenantId = authContext?.TenantId
                           ?? user.FindFirst(ClaimConstants.TenantId)?.Value
                           ?? ReadHeader(httpContext, "X-Tenant-Id")
                           ?? ReadHeader(httpContext, "TenantId");

        Guid? userId = ParseGuid(
            authContext?.CurrentUserGuid
            ?? user.FindFirst(ClaimConstants.UserIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ReadHeader(httpContext, "X-User-Id"));

        return new MControllerExecutionContext
        {
            UserId = userId,
            Username = username,
            TenantId = tenantId,
            Actor = ReadHeader(httpContext, "X-Actor") ?? username,
            IsAuthenticated = authContext?.IsAuthenticated == true || user.Identity?.IsAuthenticated == true,
            Permissions = ParsePermissions(
                authContext?.Permission
                ?? user.FindFirst(ClaimConstants.Permission)?.Value
                ?? ReadHeader(httpContext, "X-Permissions"))
        };
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
}
