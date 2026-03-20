using Muonroi.Governance.License;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.Core;
using RulesEngine.Models;
using System.Collections.Concurrent;
using System.Reflection;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Executes rules defined by Microsoft RulesEngine and maps action outputs into a <see cref="FactBag"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RulesEngineService"/> class.
/// </remarks>
/// <param name="store">The ruleset store.</param>
/// <param name="settings">The RulesEngine settings.</param>
/// <param name="licenseGuard">The license guard.</param>
/// <param name="runtimeCache">The ruleset runtime cache.</param>
/// <param name="notifier">The ruleset change notifier.</param>
/// <param name="serviceProvider">The service provider for resolving rule dependencies.</param>
public sealed class RulesEngineService(
    IRuleSetStore store,
    ReSettings? settings = null,
    ILicenseGuard? licenseGuard = null,
    IRuleSetRuntimeCache? runtimeCache = null,
    IRuleSetChangeNotifier? notifier = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly ReSettings _settings = settings ?? new ReSettings();
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;
    private readonly IRuleSetRuntimeCache? _runtimeCache = runtimeCache;
    private readonly IRuleSetChangeNotifier? _notifier = notifier;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    private static readonly ConcurrentDictionary<string, CachedWorkflowDefinition> WorkflowCache =
        new(StringComparer.OrdinalIgnoreCase);
    private const int MaxWorkflowCacheEntries = 2048;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, IReadOnlyList<Type>>> ReflectionRuleCache =
        new();

    private static readonly object ReflectionRuleCacheLock = new();
    private static int _knownAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;

    /// <summary>
    /// Saves a ruleset definition to the store and notifies of the change.
    /// </summary>
    /// <param name="workflowName">The name of the workflow/ruleset.</param>
    /// <param name="json">The JSON representation of the ruleset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveRuleSetAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        ValidateWorkflowDefinition(workflowName, json);
        await store.SaveAsync(workflowName, json, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Saved, null, cancellationToken);
    }

    /// <summary>
    /// Sets the active version of a ruleset.
    /// </summary>
    /// <param name="workflowName">The name of the workflow/ruleset.</param>
    /// <param name="version">The version number to activate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        await store.SetActiveVersionAsync(workflowName, version, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Activated, version, cancellationToken);
    }

    /// <summary>
    /// Executes the rules in a ruleset against the specified context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object.</typeparam>
    /// <param name="workflowName">The name of the workflow/ruleset.</param>
    /// <param name="context">The context object to evaluate rules against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="FactBag"/> containing the results of rule execution.</returns>
    public async Task<FactBag> ExecuteAsync<TContext>(string workflowName, TContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        string tenantId = ResolveTenantId();
        string? json = _runtimeCache is null
            ? await store.GetAsync(workflowName, cancellationToken: cancellationToken)
            : await _runtimeCache.GetOrCreateAsync(
                tenantId,
                workflowName,
                () => store.GetAsync(workflowName, cancellationToken: cancellationToken),
                cancellationToken);
        if (json is null)
        {
            return new FactBag();
        }

        CachedWorkflowDefinition definition = GetOrCreateWorkflowDefinition(tenantId, workflowName, json);
        string[]? codes = definition.RuleCodes;

        if (codes is not null)
        {
            Dictionary<string, List<IRule<TContext>>> rulesByCode = ResolveRulesByCode<TContext>(codes);
            if (rulesByCode.Count == 0)
            {
                throw new InvalidDataException("Ruleset uses code-based workflow but no rule implementations were discovered.");
            }

            List<string> missingCodes = [];
            Dictionary<string, List<string>> ambiguousCodes = new(StringComparer.OrdinalIgnoreCase);
            List<IRule<TContext>> resolvedRules = [];

            foreach (string code in codes)
            {
                if (!rulesByCode.TryGetValue(code, out List<IRule<TContext>>? candidates) || candidates.Count == 0)
                {
                    missingCodes.Add(code);
                    continue;
                }

                if (candidates.Count > 1)
                {
                    ambiguousCodes[code] = [.. candidates.Select(r => r.GetType().FullName ?? r.GetType().Name).Distinct()];
                    continue;
                }

                resolvedRules.Add(candidates[0]);
            }

            if (missingCodes.Count > 0)
            {
                throw new InvalidDataException(
                    $"Ruleset references unknown rule code(s): {string.Join(", ", missingCodes.Distinct(StringComparer.OrdinalIgnoreCase))}.");
            }

            if (ambiguousCodes.Count > 0)
            {
                string detail = string.Join(" | ",
                    ambiguousCodes.Select(kv => $"{kv.Key} => [{string.Join(", ", kv.Value)}]"));
                throw new InvalidDataException($"Ruleset contains ambiguous rule code mappings: {detail}.");
            }

            RuleEngine<TContext> orchestrator = new(licenseGuard: _licenseGuard);
            foreach (IRule<TContext> rule in resolvedRules)
            {
                orchestrator.AddRule(rule);
            }

            await orchestrator.ExecuteAsync(context, cancellationToken, Enum.GetValues<RuleType>());

            FactBag bag = new();
            PropertyInfo? resultProp = typeof(TContext).GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProp is not null)
            {
                bag["Result"] = resultProp.GetValue(context);
            }

            return bag;
        }

        // Fallback to legacy Microsoft RulesEngine JSON format
        Workflow[] workflows = definition.LegacyWorkflows ?? [];
        // Use a fresh settings instance on each execution. The RulesEngine
        // library mutates the supplied <see cref="ReSettings"/> which can drop
        // custom type registrations after the first run. Cloning preserves
        // aliases like helper classes for every invocation.
        ReSettings execSettings = new()
        {
            CustomTypes = _settings.CustomTypes?.ToArray()
        };
        RulesEngine.RulesEngine engine = new(workflows, execSettings);
        dynamic[] inputs = [new { value = context }];
        List<RuleResultTree> results = await engine.ExecuteAllRulesAsync(workflowName, inputs);

        FactBag bagLegacy = new();
        foreach (RuleResultTree result in results)
        {
            if (!string.IsNullOrEmpty(result.ExceptionMessage))
            {
                throw new Exception(result.ExceptionMessage);
            }

            if (result.ActionResult?.Exception is not null)
            {
                throw result.ActionResult.Exception;
            }

            if (result.ActionResult?.Output is IDictionary<string, object> dict)
            {
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    bagLegacy[kv.Key] = kv.Value;
                }
            }
            else if (result.ActionResult?.Output is not null)
            {
                object output = result.ActionResult.Output;
                if (output is JsonElement je)
                {
                    output = je.ValueKind switch
                    {
                        JsonValueKind.Number when je.TryGetInt32(out int i) => i,
                        JsonValueKind.Number when je.TryGetInt64(out long l) => l,
                        JsonValueKind.Number when je.TryGetDouble(out double d) => d,
                        JsonValueKind.String => je.GetString()!,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => output
                    };
                }

                bagLegacy[result.Rule.RuleName] = output;
            }
        }

        return bagLegacy;
    }

    private void EnsureRuleEngineFeature()
    {
        _licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);
    }

    private static void ValidateWorkflowDefinition(string workflowName, string json)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            throw new InvalidDataException("Workflow name is required.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Ruleset payload is empty.");
        }

        JsonElement root;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Ruleset payload is not valid JSON.", ex);
        }

        if (root.ValueKind is not JsonValueKind.Array and not JsonValueKind.Object)
        {
            throw new InvalidDataException("Ruleset payload must be a JSON object or array.");
        }

        JsonElement workflow = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().FirstOrDefault()
            : root;
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Ruleset workflow definition is invalid.");
        }

        if (workflow.TryGetProperty("WorkflowName", out JsonElement wfNameEl) &&
            wfNameEl.ValueKind == JsonValueKind.String)
        {
            string? workflowNameFromPayload = wfNameEl.GetString();
            if (!string.Equals(workflowNameFromPayload, workflowName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"WorkflowName mismatch. Expected '{workflowName}', payload has '{workflowNameFromPayload}'.");
            }
        }

        if (workflow.TryGetProperty("Rules", out JsonElement rulesEl))
        {
            if (rulesEl.ValueKind != JsonValueKind.Array || rulesEl.GetArrayLength() == 0)
            {
                throw new InvalidDataException("Rules collection must be a non-empty array.");
            }
        }
    }

    private async Task NotifyRuleChangedAsync(
        string workflowName,
        string changeType,
        int? version,
        CancellationToken cancellationToken)
    {
        string tenantId = ResolveTenantId();
        if (_runtimeCache is not null)
        {
            await _runtimeCache.InvalidateAsync(tenantId, workflowName, cancellationToken);
        }
        WorkflowCache.TryRemove(BuildWorkflowCacheKey(tenantId, workflowName), out _);

        if (_notifier is not null)
        {
            await _notifier.PublishAsync(
                new RuleSetChangeEvent(tenantId, workflowName, changeType, version, DateTimeOffset.UtcNow),
                cancellationToken);
        }
    }

    private static string ResolveTenantId()
    {
        return string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)
            ? "default"
            : TenantContext.CurrentTenantId!;
    }

    private static string BuildWorkflowCacheKey(string tenantId, string workflowName)
    {
        return $"{tenantId}:{workflowName}";
    }

    private static CachedWorkflowDefinition GetOrCreateWorkflowDefinition(string tenantId, string workflowName, string json)
    {
        string cacheKey = BuildWorkflowCacheKey(tenantId, workflowName);
        if (WorkflowCache.TryGetValue(cacheKey, out CachedWorkflowDefinition? cached) &&
            string.Equals(cached.Json, json, StringComparison.Ordinal))
        {
            return cached;
        }

        CachedWorkflowDefinition parsed = ParseWorkflowDefinition(json);
        WorkflowCache[cacheKey] = parsed;
        if (WorkflowCache.Count > MaxWorkflowCacheEntries)
        {
            WorkflowCache.Clear();
            WorkflowCache[cacheKey] = parsed;
        }

        return parsed;
    }

    private static CachedWorkflowDefinition ParseWorkflowDefinition(string json)
    {
        JsonElement? rawRoot = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            rawRoot = doc.RootElement.Clone();
            JsonElement root = rawRoot.Value;
            if (rawRoot.HasValue && rawRoot.Value.ValueKind == JsonValueKind.Array && rawRoot.Value.GetArrayLength() > 0)
            {
                root = rawRoot.Value[0];
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Rules", out JsonElement rulesEl) &&
                rulesEl.ValueKind == JsonValueKind.Array &&
                rulesEl.GetArrayLength() > 0 &&
                rulesEl[0].ValueKind == JsonValueKind.String)
            {
                List<string> codes = [];
                foreach (JsonElement el in rulesEl.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                    {
                        codes.Add(el.GetString()!);
                    }
                }

                return new CachedWorkflowDefinition(json, [.. codes], null);
            }
        }
        catch
        {
            // ignore parse errors and fall back to legacy workflow format
        }

        JsonValueKind rootKind = rawRoot?.ValueKind ?? JsonValueKind.Undefined;
        Workflow[] workflows = rootKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<Workflow[]>(json) ?? [], // MBB002-exempt: static workflow parsing — Workflow type requires direct JsonSerializer
            JsonValueKind.Object => JsonSerializer.Deserialize<Workflow>(json) is Workflow single ? [single] : [], // MBB002-exempt: static workflow parsing
            _ => []
        };
        return new CachedWorkflowDefinition(json, null, workflows);
    }

    private Dictionary<string, List<IRule<TContext>>> ResolveRulesByCode<TContext>(IReadOnlyList<string> requestedCodes)
    {
        Dictionary<string, List<IRule<TContext>>> rulesByCode = new(StringComparer.OrdinalIgnoreCase);

        if (_serviceProvider is not null)
        {
            foreach (IRule<TContext> rule in _serviceProvider.GetServices<IRule<TContext>>())
            {
                AddRuleInstance(rulesByCode, rule);
            }
        }

        string[] unresolvedCodes = [.. requestedCodes
            .Where(code => !rulesByCode.ContainsKey(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (unresolvedCodes.Length == 0)
        {
            return rulesByCode;
        }

        IReadOnlyDictionary<string, IReadOnlyList<Type>> discovered = GetOrCreateReflectionRuleMap<TContext>();
        foreach (string? code in unresolvedCodes)
        {
            if (!discovered.TryGetValue(code, out IReadOnlyList<Type>? candidates))
            {
                continue;
            }

            foreach (Type candidate in candidates)
            {
                if (TryCreateRuleInstance(candidate) is IRule<TContext> instance)
                {
                    AddRuleInstance(rulesByCode, instance);
                }
            }
        }

        return rulesByCode;
    }

    private object? TryCreateRuleInstance(Type ruleType)
    {
        if (_serviceProvider is not null)
        {
            try
            {
                return ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, ruleType);
            }
            catch
            {
                // fallback to parameterless activator below
            }
        }

        try
        {
            return Activator.CreateInstance(ruleType, true);
        }
        catch
        {
            return null;
        }
    }

    private static void AddRuleInstance<TContext>(
        Dictionary<string, List<IRule<TContext>>> map,
        IRule<TContext> rule)
    {
        if (!map.TryGetValue(rule.Code, out List<IRule<TContext>>? rules))
        {
            rules = [];
            map[rule.Code] = rules;
        }

        if (rules.Any(existing => existing.GetType() == rule.GetType()))
        {
            return;
        }

        rules.Add(rule);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Type>> GetOrCreateReflectionRuleMap<TContext>()
    {
        EnsureReflectionRuleCacheCurrent();
        return ReflectionRuleCache.GetOrAdd(typeof(TContext), _ => DiscoverRuleTypes<TContext>());
    }

    private static void EnsureReflectionRuleCacheCurrent()
    {
        int currentAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
        if (currentAssemblyCount == Volatile.Read(ref _knownAssemblyCount))
        {
            return;
        }

        lock (ReflectionRuleCacheLock)
        {
            currentAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
            if (currentAssemblyCount == _knownAssemblyCount)
            {
                return;
            }

            ReflectionRuleCache.Clear();
            _knownAssemblyCount = currentAssemblyCount;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Type>> DiscoverRuleTypes<TContext>()
    {
        Type contextType = typeof(TContext);
        Type ruleInterface = typeof(IRule<>).MakeGenericType(contextType);
        Type[] contextArgs = contextType.IsGenericType ? contextType.GetGenericArguments() : Type.EmptyTypes;
        Dictionary<string, HashSet<Type>> discovered = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                Type? candidate = null;
                if (type.IsGenericTypeDefinition)
                {
                    if (contextArgs.Length != type.GetGenericArguments().Length)
                    {
                        continue;
                    }

                    try
                    {
                        candidate = type.MakeGenericType(contextArgs);
                    }
                    catch
                    {
                        continue;
                    }
                }
                else
                {
                    candidate = type;
                }

                if (candidate is null || !ruleInterface.IsAssignableFrom(candidate))
                {
                    continue;
                }

                IRule<TContext>? probe;
                try
                {
                    probe = Activator.CreateInstance(candidate, true) as IRule<TContext>;
                }
                catch
                {
                    continue;
                }

                if (probe is null || string.IsNullOrWhiteSpace(probe.Code))
                {
                    continue;
                }

                if (!discovered.TryGetValue(probe.Code, out HashSet<Type>? types))
                {
                    types = [];
                    discovered[probe.Code] = types;
                }

                types.Add(candidate);
            }
        }

        return discovered.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Type>)[.. pair.Value.OrderBy(t => t.FullName, StringComparer.Ordinal)],
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
        catch
        {
            return [];
        }
    }

    private sealed record CachedWorkflowDefinition(
        string Json,
        string[]? RuleCodes,
        Workflow[]? LegacyWorkflows);
}
