using Microsoft.AspNetCore.Mvc;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Controllers;

/// <summary>
/// Returns rule catalog data in the format compatible with
/// the <c>mu-rule-flow-designer</c> Palette (<c>MRuleCatalogService</c> frontend).
/// Groups scanned <see cref="MUiEngineCatalogRuleDescriptor"/> items by context type
/// and maps them to <see cref="MRuleCatalogGroupResponse"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="MRuleCatalogCompatController"/>.
/// </remarks>
[ApiController]
[Route("api/v1/ui-engine/catalog/palette")]
public class MRuleCatalogCompatController(ICatalogScanService scanService) : ControllerBase
{
    private readonly ICatalogScanService _scanService = scanService;

    /// <summary>
    /// List catalog items grouped by category, compatible with <c>MRuleCatalogService.MListCatalog()</c>.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MRuleCatalogGroupResponse>>> MListCatalog(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var rules = await _scanService.ScanRulesAsync(ct);

        var items = rules
            .Where(r => r.IsEnabled)
            .Select(r => new MRuleCatalogItemResponse(
                Code: r.Code,
                DisplayName: !string.IsNullOrWhiteSpace(r.Name) ? r.Name : r.Code,
                Category: !string.IsNullOrWhiteSpace(r.ContextType) ? r.ContextType : "Rules",
                Description: $"Order: {r.Order}, HookPoint: {r.HookPoint}, Type: {r.RuleType}",
                Icon: null,
                Tags: new List<string>
                {
                    r.RuleType,
                    r.Source,
                    r.HookPoint
                }.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(),
                InputSchema: null,
                OutputSchema: null
            ));

        if (!string.IsNullOrWhiteSpace(category))
        {
            items = items.Where(i =>
                string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            items = items.Where(i =>
                i.Code.ToLowerInvariant().Contains(q) ||
                i.DisplayName.ToLowerInvariant().Contains(q) ||
                (i.Description?.ToLowerInvariant().Contains(q) == true));
        }

        var groups = items
            .GroupBy(i => i.Category ?? "Uncategorized")
            .OrderBy(g => g.Key)
            .Select(g => new MRuleCatalogGroupResponse(
                Category: g.Key,
                Items: g.OrderBy(i => i.DisplayName).ToList()))
            .ToList();

        return Ok(groups);
    }

    /// <summary>
    /// Get a specific catalog item by code.
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<MRuleCatalogItemResponse>> MGetItem(
        string code, CancellationToken ct = default)
    {
        var rules = await _scanService.ScanRulesAsync(ct);
        var match = rules.FirstOrDefault(r =>
            string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));

        if (match is null) return NotFound();

        return Ok(new MRuleCatalogItemResponse(
            Code: match.Code,
            DisplayName: !string.IsNullOrWhiteSpace(match.Name) ? match.Name : match.Code,
            Category: !string.IsNullOrWhiteSpace(match.ContextType) ? match.ContextType : "Rules",
            Description: $"Order: {match.Order}, HookPoint: {match.HookPoint}, Type: {match.RuleType}",
            Icon: null,
            Tags: new List<string>
            {
                match.RuleType,
                match.Source,
                match.HookPoint
            }.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(),
            InputSchema: null,
            OutputSchema: null
        ));
    }
}
