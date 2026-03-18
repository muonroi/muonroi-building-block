using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.RuleEngine.Abstractions.Authoring;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Models;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Default implementation that uses <see cref="MRuleAuthoringManifestRegistry"/> to resolve
/// rule contract schemas. Falls back to reflection-based schema building when the registry
/// does not contain a pre-built context schema.
/// </summary>
public class MDefaultRuleFlowContractProvider : IMRuleFlowContractProvider
{
    private readonly MRuleAuthoringManifestRegistry _registry;
    private readonly RulesEngineService? _rulesEngine;
    private readonly ILogger<MDefaultRuleFlowContractProvider>? _log;

    /// <summary>
    /// Lazy-built index: rule code → authoring entry (case-insensitive).
    /// </summary>
    private Dictionary<string, MRuleAuthoringEntry>? _ruleIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="MDefaultRuleFlowContractProvider"/>.
    /// </summary>
    public MDefaultRuleFlowContractProvider(
        MRuleAuthoringManifestRegistry registry,
        RulesEngineService? rulesEngine = null,
        ILogger<MDefaultRuleFlowContractProvider>? log = null)
    {
        _registry = registry;
        _rulesEngine = rulesEngine;
        _log = log;
    }

    /// <inheritdoc />
    public Task<MRuleFlowContractLookupResponse?> MGetContractAsync(
        string sourceType, string sourceCode, CancellationToken ct = default)
    {
        MRuleAuthoringEntry? entry = FindEntryByCode(sourceCode);
        if (entry is null)
        {
            _log?.LogDebug("No rule entry found in manifest registry for code {Code}", sourceCode);
            return Task.FromResult<MRuleFlowContractLookupResponse?>(null);
        }

        MRuleContractSchema? requestSchema = MapContextSchema(entry, $"{sourceCode}.Request");
        MRuleContractSchema? responseSchema = BuildOutputSchema(entry, $"{sourceCode}.Response");

        return Task.FromResult<MRuleFlowContractLookupResponse?>(
            new MRuleFlowContractLookupResponse(sourceType, sourceCode, requestSchema, responseSchema));
    }

    /// <inheritdoc />
    public async Task<MRuleFlowContractLookupResponse?> MGetFlowContractAsync(
        string flowCode, CancellationToken ct = default)
    {
        string? firstRuleCode = await FindFirstRuleCodeInFlow(flowCode, ct);
        if (firstRuleCode is null)
        {
            return new MRuleFlowContractLookupResponse("flow", flowCode, null, null);
        }

        MRuleAuthoringEntry? entry = FindEntryByCode(firstRuleCode);
        if (entry is null)
        {
            return new MRuleFlowContractLookupResponse("flow", flowCode, null, null);
        }

        MRuleContractSchema? requestSchema = MapContextSchema(entry, $"{flowCode}.Request");
        MRuleContractSchema? responseSchema = BuildOutputSchema(entry, $"{flowCode}.Response");

        return new MRuleFlowContractLookupResponse("flow", flowCode, requestSchema, responseSchema);
    }

    /// <inheritdoc />
    public async Task<MRuleFlowNodeContractResponse?> MGetNodeAuthoringContractAsync(
        string flowCode, string nodeId, CancellationToken ct = default)
    {
        string? ruleCode = await FindRuleCodeForNode(flowCode, nodeId, ct);
        if (ruleCode is null)
        {
            return new MRuleFlowNodeContractResponse(nodeId, flowCode, null, null);
        }

        MRuleAuthoringEntry? entry = FindEntryByCode(ruleCode);
        if (entry is null)
        {
            return new MRuleFlowNodeContractResponse(nodeId, flowCode, null, null);
        }

        MRuleContractSchema? requestSchema = MapContextSchema(entry, $"{ruleCode}.Request");
        MRuleContractSchema? responseSchema = BuildOutputSchema(entry, $"{ruleCode}.Response");
        List<string>? availableInputKeys = entry.ConsumedFacts.Count > 0
            ? entry.ConsumedFacts.Select(f => f.Key).ToList()
            : null;

        return new MRuleFlowNodeContractResponse(nodeId, flowCode, requestSchema, responseSchema, availableInputKeys);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MRuleFlowSummary>> MListFlowsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<MRuleFlowSummary>>(Array.Empty<MRuleFlowSummary>());
    }

    private MRuleAuthoringEntry? FindEntryByCode(string code)
    {
        _ruleIndex ??= BuildRuleIndex();
        _ruleIndex.TryGetValue(code, out MRuleAuthoringEntry? entry);
        return entry;
    }

    private Dictionary<string, MRuleAuthoringEntry> BuildRuleIndex()
    {
        Dictionary<string, MRuleAuthoringEntry> index = new(StringComparer.OrdinalIgnoreCase);
        foreach (MRuleAuthoringManifest manifest in _registry.GetManifests())
        {
            foreach (MRuleAuthoringEntry rule in manifest.Rules)
            {
                index.TryAdd(rule.Code, rule);
            }
        }

        return index;
    }

    /// <summary>
    /// Maps <see cref="MRuleAuthoringEntry.ContextSchema"/> fields to <see cref="MRuleContractSchema"/>.
    /// </summary>
    private static MRuleContractSchema? MapContextSchema(MRuleAuthoringEntry entry, string contractName)
    {
        if (entry.ContextSchema is null || entry.ContextSchema.Fields.Count == 0)
        {
            return null;
        }

        List<MRuleContractField> fields = MapSchemaFields(entry.ContextSchema.Fields);
        return fields.Count > 0 ? new MRuleContractSchema(contractName, fields) : null;
    }

    /// <summary>
    /// Builds an output schema from <see cref="MRuleAuthoringEntry.ProducedFacts"/>.
    /// </summary>
    private static MRuleContractSchema? BuildOutputSchema(MRuleAuthoringEntry entry, string contractName)
    {
        if (entry.ProducedFacts.Count == 0)
        {
            return null;
        }

        List<MRuleContractField> fields = [];
        foreach (MFactEntry fact in entry.ProducedFacts)
        {
            IReadOnlyList<MRuleContractField>? children = fact.Schema is not null
                ? MapSchemaFields(fact.Schema.Fields)
                : null;

            fields.Add(new MRuleContractField(
                fact.Key,
                fact.ClrTypeName ?? "object",
                IsRequired: false,
                Description: fact.Description,
                Children: children is { Count: > 0 } ? children : null));
        }

        return fields.Count > 0 ? new MRuleContractSchema(contractName, fields) : null;
    }

    private static List<MRuleContractField> MapSchemaFields(IReadOnlyList<MFactSchemaField> source)
    {
        List<MRuleContractField> result = new(source.Count);
        foreach (MFactSchemaField field in source)
        {
            IReadOnlyList<MRuleContractField>? children = field.Children.Count > 0
                ? MapSchemaFields(field.Children)
                : null;

            result.Add(new MRuleContractField(
                field.Label,
                field.DataType,
                IsRequired: field.Required,
                Description: field.Description,
                Children: children is { Count: > 0 } ? children : null));
        }

        return result;
    }

    /// <summary>
    /// Loads a flow definition JSON and returns the ruleCode of the first RuleTask node.
    /// </summary>
    private async Task<string?> FindFirstRuleCodeInFlow(string flowCode, CancellationToken ct)
    {
        JsonElement? flowGraph = await LoadFlowGraph(flowCode, ct);
        if (flowGraph is null)
        {
            return null;
        }

        if (!flowGraph.Value.TryGetProperty("nodes", out JsonElement nodes))
        {
            return null;
        }

        foreach (JsonElement node in nodes.EnumerateArray())
        {
            string? ruleCode = ExtractRuleCode(node);
            if (ruleCode is not null)
            {
                return ruleCode;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads a flow definition JSON and finds the ruleCode for a specific node by ID.
    /// </summary>
    private async Task<string?> FindRuleCodeForNode(string flowCode, string nodeId, CancellationToken ct)
    {
        JsonElement? flowGraph = await LoadFlowGraph(flowCode, ct);
        if (flowGraph is null)
        {
            return null;
        }

        if (!flowGraph.Value.TryGetProperty("nodes", out JsonElement nodes))
        {
            return null;
        }

        foreach (JsonElement node in nodes.EnumerateArray())
        {
            if (node.TryGetProperty("id", out JsonElement idEl) &&
                string.Equals(idEl.GetString(), nodeId, StringComparison.OrdinalIgnoreCase))
            {
                return ExtractRuleCode(node);
            }
        }

        return null;
    }

    private async Task<JsonElement?> LoadFlowGraph(string flowCode, CancellationToken ct)
    {
        if (_rulesEngine is null)
        {
            _log?.LogDebug("RulesEngineService not available, cannot resolve flow {FlowCode}", flowCode);
            return null;
        }

        string? json = await _rulesEngine.GetRuleSetAsync(flowCode, cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            // The flow graph is nested under "flowGraph" in the ruleset envelope
            if (doc.RootElement.TryGetProperty("flowGraph", out JsonElement flowGraph))
            {
                // Clone so we can dispose the document
                return flowGraph.Clone();
            }

            return null;
        }
        catch (JsonException ex)
        {
            _log?.LogDebug(ex, "Failed to parse flow definition for {FlowCode}", flowCode);
            return null;
        }
    }

    /// <summary>
    /// Extracts a ruleCode from a flow node JSON element.
    /// Checks both top-level "ruleCode" and nested "data.ruleCode".
    /// </summary>
    private static string? ExtractRuleCode(JsonElement node)
    {
        if (node.TryGetProperty("ruleCode", out JsonElement topCode))
        {
            string? val = topCode.GetString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        if (node.TryGetProperty("data", out JsonElement data) &&
            data.TryGetProperty("ruleCode", out JsonElement dataCode))
        {
            string? val = dataCode.GetString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        return null;
    }
}
