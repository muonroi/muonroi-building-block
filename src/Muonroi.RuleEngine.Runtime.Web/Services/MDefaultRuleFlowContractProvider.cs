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
    private readonly ILogger<MDefaultRuleFlowContractProvider>? _log;

    /// <summary>
    /// Lazy-built index: rule code → authoring entry (case-insensitive).
    /// </summary>
    private Dictionary<string, MRuleAuthoringEntry>? _ruleIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="MDefaultRuleFlowContractProvider"/>.
    /// Resolves <see cref="MRuleAuthoringManifestRegistry"/> from the service provider,
    /// creating a new instance if one is not registered.
    /// </summary>
    public MDefaultRuleFlowContractProvider(
        IServiceProvider serviceProvider,
        ILogger<MDefaultRuleFlowContractProvider>? log = null)
    {
        _registry = serviceProvider.GetService(typeof(MRuleAuthoringManifestRegistry)) as MRuleAuthoringManifestRegistry
                    ?? new MRuleAuthoringManifestRegistry(serviceProvider);
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
    public Task<MRuleFlowContractLookupResponse?> MGetFlowContractAsync(
        string flowCode, CancellationToken ct = default)
    {
        // Strategy: find rules belonging to this flow by checking the flow JSON,
        // then fall back to manifest-only lookup if RulesEngineService is unavailable.
        // All rules in a flow share the same TContext, so any rule's schema works.
        MRuleAuthoringEntry? entry = FindEntryForFlow(flowCode);
        if (entry is null)
        {
            _log?.LogDebug("No manifest entry found for flow {FlowCode}", flowCode);
            return Task.FromResult<MRuleFlowContractLookupResponse?>(
                new MRuleFlowContractLookupResponse("flow", flowCode, null, null));
        }

        MRuleContractSchema? requestSchema = MapContextSchema(entry, $"{flowCode}.Request");
        MRuleContractSchema? responseSchema = BuildOutputSchema(entry, $"{flowCode}.Response");

        return Task.FromResult<MRuleFlowContractLookupResponse?>(
            new MRuleFlowContractLookupResponse("flow", flowCode, requestSchema, responseSchema));
    }

    /// <inheritdoc />
    public Task<MRuleFlowNodeContractResponse?> MGetNodeAuthoringContractAsync(
        string flowCode, string nodeId, CancellationToken ct = default)
    {
        // Try direct lookup: nodeId may contain or match a rule code
        MRuleAuthoringEntry? entry = FindEntryByCode(nodeId);

        // Fallback: strip common prefixes like "node-rule-" and try matching
        if (entry is null && nodeId.StartsWith("node-rule-", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = nodeId["node-rule-".Length..];
            entry = FindEntryByCodePrefix(flowCode, suffix);
        }

        // Fallback: use the flow's shared context (all rules share TContext)
        entry ??= FindEntryForFlow(flowCode);

        if (entry is null)
        {
            return Task.FromResult<MRuleFlowNodeContractResponse?>(
                new MRuleFlowNodeContractResponse(nodeId, flowCode, null, null));
        }

        MRuleContractSchema? requestSchema = MapContextSchema(entry, $"{entry.Code}.Request");
        MRuleContractSchema? responseSchema = BuildOutputSchema(entry, $"{entry.Code}.Response");
        List<string>? availableInputKeys = entry.ConsumedFacts.Count > 0
            ? [.. entry.ConsumedFacts.Select(f => f.Key)]
            : null;

        return Task.FromResult<MRuleFlowNodeContractResponse?>(
            new MRuleFlowNodeContractResponse(nodeId, flowCode, requestSchema, responseSchema, availableInputKeys));
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

    /// <summary>
    /// Finds a rule entry whose code starts with a flow-derived prefix (e.g. "FCD_V4_" from "FCD_V4_RULES")
    /// and whose suffix contains the given hint (e.g. "barge" matches "FCD_V4_BARGE_VALID").
    /// </summary>
    private MRuleAuthoringEntry? FindEntryByCodePrefix(string flowCode, string hint)
    {
        _ruleIndex ??= BuildRuleIndex();
        foreach (KeyValuePair<string, MRuleAuthoringEntry> kvp in _ruleIndex)
        {
            if (kvp.Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a manifest entry for a flow by matching rules whose code shares a common prefix
    /// with the flow code. For example, flow "FCD_V4_RULES" matches rules "FCD_V4_LINER_VALID",
    /// "FCD_V4_PORT_VALID", etc. All rules in a flow share the same TContext, so any match works.
    /// </summary>
    private MRuleAuthoringEntry? FindEntryForFlow(string flowCode)
    {
        _ruleIndex ??= BuildRuleIndex();

        // Strategy 1: Extract common prefix from flowCode (e.g. "FCD_V4_" from "FCD_V4_RULES")
        string prefix = ExtractFlowPrefix(flowCode);
        if (!string.IsNullOrEmpty(prefix))
        {
            foreach (KeyValuePair<string, MRuleAuthoringEntry> kvp in _ruleIndex)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(kvp.Key, flowCode, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        // Strategy 2: If only one context type exists across all rules, use that
        MRuleAuthoringEntry? firstWithSchema = null;
        foreach (KeyValuePair<string, MRuleAuthoringEntry> kvp in _ruleIndex)
        {
            if (kvp.Value.ContextSchema is { Fields.Count: > 0 })
            {
                firstWithSchema ??= kvp.Value;
            }
        }

        return firstWithSchema;
    }

    /// <summary>
    /// Extracts a common prefix from a flow code by removing the last segment.
    /// "FCD_V4_RULES" → "FCD_V4_", "MY_FLOW" → "MY_".
    /// </summary>
    private static string ExtractFlowPrefix(string flowCode)
    {
        int lastUnderscore = flowCode.LastIndexOf('_');
        return lastUnderscore > 0 ? flowCode[..(lastUnderscore + 1)] : string.Empty;
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
                Path: fact.Key,
                Label: fact.Label ?? fact.Key,
                DataType: fact.ClrTypeName ?? "object",
                Required: false,
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
                Path: field.Path,
                Label: field.Label,
                DataType: field.DataType,
                Required: field.Required,
                Description: field.Description,
                Children: children is { Count: > 0 } ? children : null));
        }

        return result;
    }

}
