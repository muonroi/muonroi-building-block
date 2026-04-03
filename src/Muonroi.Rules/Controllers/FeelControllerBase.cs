using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Rules.Feel;

namespace Muonroi.Rules.Controllers;

/// <summary>
/// Base controller for FEEL operations, providing core evaluation and autocomplete logic.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public abstract class FeelControllerBase : ControllerBase
{
    /// <summary>
    /// Evaluates a FEEL expression with the provided context.
    /// </summary>
    /// <param name="request">The evaluation request.</param>
    /// <returns>An <see cref="IActionResult"/> containing the evaluation result.</returns>
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

    /// <summary>
    /// Provides autocompletion suggestions for a partial FEEL expression.
    /// </summary>
    /// <param name="request">The autocomplete request.</param>
    /// <returns>An <see cref="IActionResult"/> containing the suggestions.</returns>
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

    /// <summary>
    /// Returns examples of FEEL expressions.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing the examples.</returns>
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

    /// <summary>
    /// Returns a list of FEEL keywords.
    /// </summary>
    /// <returns>A list of keywords.</returns>
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

    /// <summary>
    /// Returns numeric FEEL expression examples.
    /// </summary>
    /// <returns>A list of numeric examples.</returns>
    protected virtual IReadOnlyList<string> GetNumericExamples() =>
    [
        "amount > 1000",
        "score in [80..100]",
        "if score >= 90 then \"A\" else \"B\""
    ];

    /// <summary>
    /// Returns string FEEL expression examples.
    /// </summary>
    /// <returns>A list of string examples.</returns>
    protected virtual IReadOnlyList<string> GetStringExamples() =>
    [
        "customerType in (\"vip\", \"gold\")",
        "country matches \"^(US|CA)$\""
    ];

    /// <summary>
    /// Returns list and context FEEL expression examples.
    /// </summary>
    /// <returns>A list of examples.</returns>
    protected virtual IReadOnlyList<string> GetListExamples() =>
    [
        "for x in [1..5] return x * 2",
        "some item in items satisfies item.price > 100"
    ];

    /// <summary>
    /// Extracts the last token from a partial FEEL expression.
    /// </summary>
    /// <param name="value">The expression string.</param>
    /// <returns>The last token extracted.</returns>
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

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to its corresponding .NET object.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>The converted object.</returns>
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

    /// <summary>
    /// Resolves the <see cref="MControllerExecutionContext"/> for the current request.
    /// </summary>
    /// <returns>The execution context, or null if it cannot be resolved.</returns>
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
