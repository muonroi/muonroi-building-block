namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Executes rules defined by Microsoft RulesEngine and maps action outputs into a <see cref="FactBag"/>.
/// </summary>
public sealed class RulesEngineService(
    IRuleSetStore store,
    ReSettings? settings = null,
    ILicenseGuard? licenseGuard = null,
    IRuleSetRuntimeCache? runtimeCache = null,
    IRuleSetChangeNotifier? notifier = null,
    IServiceProvider? serviceProvider = null,
    IRuleSetDefinitionValidator? validator = null,
    ICanaryRolloutService? canaryRolloutService = null)
{
    private readonly ReSettings _settings = settings ?? new ReSettings();
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;
    private readonly IRuleSetRuntimeCache? _runtimeCache = runtimeCache;
    private readonly IRuleSetChangeNotifier? _notifier = notifier;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    private readonly IRuleSetDefinitionValidator _validator = validator ?? new RuleSetDefinitionValidator();
    private readonly ICanaryRolloutService? _canaryRolloutService = canaryRolloutService;

    private static readonly ConcurrentDictionary<string, CachedWorkflowDefinition> WorkflowCache =
        new(StringComparer.OrdinalIgnoreCase);
    private const int MaxWorkflowCacheEntries = 2048;

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, IReadOnlyList<Type>>> ReflectionRuleCache =
        new();

    private static readonly object ReflectionRuleCacheLock = new();
    private static int _knownAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;

    public async Task SaveRuleSetAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        _validator.Validate(workflowName, json).ThrowIfInvalid();
        await store.SaveAsync(workflowName, json, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Saved, null, cancellationToken);
    }

    public async Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        await store.SetActiveVersionAsync(workflowName, version, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Activated, version, cancellationToken);
    }

    public Task<RuleSetValidationResult> ValidateRuleSetAsync(string workflowName, string json,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnsureRuleEngineFeature();
        RuleSetValidationResult result = _validator.Validate(workflowName, json);
        return Task.FromResult(result);
    }

    public async Task<string?> GetRuleSetAsync(string workflowName, int? version = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetAsync(workflowName, version, cancellationToken);
    }

    public async Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetVersionsAsync(workflowName, cancellationToken);
    }

    public async Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetActiveVersionAsync(workflowName, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetWorkflowsAsync(cancellationToken);
    }

    public async Task<FactBag> ExecuteAsync<TContext>(string workflowName, TContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        string tenantId = ResolveTenantId();

        int? canaryVersion = _canaryRolloutService is null
            ? null
            : await _canaryRolloutService.GetCanaryVersionForTenantAsync(workflowName, tenantId, cancellationToken);

        string? json;
        if (canaryVersion.HasValue)
        {
            json = await store.GetAsync(workflowName, canaryVersion.Value, cancellationToken);
        }
        else
        {
            json = _runtimeCache is null
                ? await store.GetAsync(workflowName, cancellationToken: cancellationToken)
                : await _runtimeCache.GetOrCreateAsync(
                    tenantId,
                    workflowName,
                    () => store.GetAsync(workflowName, cancellationToken: cancellationToken),
                    cancellationToken);
        }

        if (json is null) return new FactBag();

        CachedWorkflowDefinition definition = GetOrCreateWorkflowDefinition(tenantId, workflowName, json);
        string[]? codes = definition.RuleCodes;

        if (codes is not null)
        {
            return await ExecuteCodeWorkflowAsync(codes, context, definition.ExecutionMode, cancellationToken);
        }

        // Fallback to legacy Microsoft RulesEngine JSON format
        Workflow[] workflows = definition.LegacyWorkflows ?? [];
        return await ExecuteLegacyWorkflowAsync(workflowName, workflows, context);
    }

    public async Task<FactBag> DryRunAsync(
        string workflowName,
        string json,
        JsonElement context,
        string? contextType = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        _validator.Validate(workflowName, json).ThrowIfInvalid();

        CachedWorkflowDefinition definition = ParseWorkflowDefinition(json);
        if (definition.RuleCodes is not null)
        {
            if (string.IsNullOrWhiteSpace(contextType))
            {
                throw new InvalidDataException(
                    "Code-based ruleset dry-run requires 'contextType' (assembly-qualified name or full type name).");
            }

            Type resolved = ResolveContextType(contextType);
            object? contextValue = JsonSerializer.Deserialize(context.GetRawText(), resolved, // MBB002-exempt: requires Type-based overload with custom options not available in wrapper
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (contextValue is null)
            {
                throw new InvalidDataException("Dry-run context payload could not be deserialized to the requested contextType.");
            }

            return await ExecuteCodeWorkflowDynamicAsync(
                definition.RuleCodes,
                contextValue,
                definition.ExecutionMode,
                cancellationToken);
        }

        object? contextValueLegacy = ConvertJsonElement(context);
        if (contextValueLegacy is null)
        {
            return await ExecuteLegacyWorkflowAsync<object?>(workflowName, definition.LegacyWorkflows ?? [], null);
        }

        return await ExecuteLegacyWorkflowDynamicAsync(workflowName, definition.LegacyWorkflows ?? [], contextValueLegacy);
    }

    private void EnsureRuleEngineFeature()
    {
        _licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);
    }

    private async Task<FactBag> ExecuteCodeWorkflowAsync<TContext>(
        IReadOnlyList<string> codes,
        TContext context,
        ExecutionMode? executionMode,
        CancellationToken cancellationToken)
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
            string detail = string.Join(" | ", ambiguousCodes.Select(kv => $"{kv.Key} => [{string.Join(", ", kv.Value)}]"));
            throw new InvalidDataException($"Ruleset contains ambiguous rule code mappings: {detail}.");
        }

        IEnumerable<IHookHandler<TContext>> hooks = _serviceProvider?.GetServices<IHookHandler<TContext>>() ?? [];
        IEnumerable<IRuleEventListener<TContext>> listeners =
            _serviceProvider?.GetServices<IRuleEventListener<TContext>>() ?? [];
        Microsoft.Extensions.Logging.ILogger<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>? logger =
            _serviceProvider?.GetService<Microsoft.Extensions.Logging.ILogger<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>>();
        ITenantQuotaTracker? quotaTracker = _serviceProvider?.GetService<ITenantQuotaTracker>();
        IRuleExecutionTracer? tracer = _serviceProvider?.GetService<IRuleExecutionTracer>();
        ISystemExecutionContextAccessor? contextAccessor = _serviceProvider?.GetService<ISystemExecutionContextAccessor>();

        Muonroi.RuleEngine.Core.RuleOrchestrator<TContext> orchestrator =
            new(resolvedRules, hooks, logger, listeners, quotaTracker, tracer, contextAccessor);
        OrchestratorResult execution = await orchestrator.ExecuteWithResultAsync(
            context,
            executionMode ?? ExecutionMode.AllOrNothing,
            cancellationToken: cancellationToken);

        if (!execution.IsSuccess)
        {
            string message = execution.Errors.Count > 0
                ? string.Join("; ", execution.Errors)
                : "Rule orchestration failed.";
            if (execution.CompensationErrors.Count > 0)
            {
                message = $"{message} Compensation: {string.Join("; ", execution.CompensationErrors)}";
            }

            throw new InvalidOperationException(message);
        }

        return execution.Facts;
    }

    private async Task<FactBag> ExecuteCodeWorkflowDynamicAsync(
        IReadOnlyList<string> codes,
        object context,
        ExecutionMode? executionMode,
        CancellationToken cancellationToken)
    {
        MethodInfo? bridge = GetType().GetMethod(
            nameof(ExecuteCodeWorkflowBridgeAsync),
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (bridge is null)
        {
            throw new MissingMethodException(nameof(ExecuteCodeWorkflowBridgeAsync));
        }

        MethodInfo closed = bridge.MakeGenericMethod(context.GetType());
        Task<FactBag>? invoke = closed.Invoke(this, [codes, context, executionMode, cancellationToken]) as Task<FactBag>;
        if (invoke is null)
        {
            throw new InvalidOperationException("Unable to invoke code-based dry-run bridge.");
        }

        return await invoke;
    }

    private Task<FactBag> ExecuteCodeWorkflowBridgeAsync<TContext>(
        IReadOnlyList<string> codes,
        object context,
        ExecutionMode? executionMode,
        CancellationToken cancellationToken)
    {
        if (context is not TContext typed)
        {
            throw new InvalidDataException($"Dry-run context type mismatch. Expected '{typeof(TContext).FullName}'.");
        }

        return ExecuteCodeWorkflowAsync(codes, typed, executionMode, cancellationToken);
    }

    private async Task<FactBag> ExecuteLegacyWorkflowDynamicAsync(
        string workflowName,
        Workflow[] workflows,
        object context)
    {
        MethodInfo? bridge = GetType().GetMethod(
            nameof(ExecuteLegacyWorkflowBridgeAsync),
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (bridge is null)
        {
            throw new MissingMethodException(nameof(ExecuteLegacyWorkflowBridgeAsync));
        }

        MethodInfo closed = bridge.MakeGenericMethod(context.GetType());
        Task<FactBag>? invoke = closed.Invoke(this, [workflowName, workflows, context]) as Task<FactBag>;
        if (invoke is null)
        {
            throw new InvalidOperationException("Unable to invoke legacy dry-run bridge.");
        }

        return await invoke;
    }

    private Task<FactBag> ExecuteLegacyWorkflowBridgeAsync<TContext>(
        string workflowName,
        Workflow[] workflows,
        object context)
    {
        if (context is not TContext typed)
        {
            throw new InvalidDataException($"Legacy dry-run context type mismatch. Expected '{typeof(TContext).FullName}'.");
        }

        return ExecuteLegacyWorkflowAsync(workflowName, workflows, typed);
    }

    private async Task<FactBag> ExecuteLegacyWorkflowAsync<TContext>(
        string workflowName,
        Workflow[] workflows,
        TContext context)
    {
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
                bagLegacy[result.Rule.RuleName] = NormalizeJsonOutput(result.ActionResult.Output);
            }
        }

        return bagLegacy;
    }

    private static object NormalizeJsonOutput(object output)
    {
        if (output is not JsonElement je)
        {
            return output;
        }

        return je.ValueKind switch
        {
            JsonValueKind.Number when je.TryGetInt32(out int i) => i,
            JsonValueKind.Number when je.TryGetInt64(out long l) => l,
            JsonValueKind.Number when je.TryGetDouble(out double d) => d,
            JsonValueKind.String => je.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => je.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => je.EnumerateObject().ToDictionary(x => x.Name, x => ConvertJsonElement(x.Value), StringComparer.OrdinalIgnoreCase),
            _ => output
        };
    }

    private static Type ResolveContextType(string contextTypeName)
    {
        Type? resolved = Type.GetType(contextTypeName, throwOnError: false, ignoreCase: true);
        if (resolved is not null)
        {
            return resolved;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            resolved = assembly.GetType(contextTypeName, throwOnError: false, ignoreCase: true);
            if (resolved is not null)
            {
                return resolved;
            }

            resolved = GetLoadableTypes(assembly).FirstOrDefault(x =>
                string.Equals(x.FullName, contextTypeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Name, contextTypeName, StringComparison.OrdinalIgnoreCase));
            if (resolved is not null)
            {
                return resolved;
            }
        }

        throw new InvalidDataException($"Cannot resolve contextType '{contextTypeName}'.");
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined => null,
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out int i) => i,
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out decimal dec) => dec,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                x => x.Name,
                x => ConvertJsonElement(x.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
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
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement rawRoot = doc.RootElement.Clone();
            JsonElement root = rawRoot;
            if (rawRoot.ValueKind == JsonValueKind.Array && rawRoot.GetArrayLength() > 0)
            {
                root = rawRoot[0];
            }

            if (root.ValueKind == JsonValueKind.Object &&
                (root.TryGetProperty("Rules", out JsonElement rulesEl) || root.TryGetProperty("rules", out rulesEl)) &&
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

                return new CachedWorkflowDefinition(json, [.. codes], null, ParseExecutionMode(root));
            }

            Workflow[] workflows = rawRoot.ValueKind switch
            {
                JsonValueKind.Array => JsonSerializer.Deserialize<Workflow[]>(json) ?? [], // MBB002-exempt: static workflow parsing — Workflow type requires direct JsonSerializer
                JsonValueKind.Object => JsonSerializer.Deserialize<Workflow>(json) is Workflow single ? [single] : [], // MBB002-exempt: static workflow parsing
                _ => []
            };
            return new CachedWorkflowDefinition(json, null, workflows, ParseExecutionMode(root));
        }
        catch
        {
            // ignore parse errors and fall back to legacy workflow format
        }

        return new CachedWorkflowDefinition(json, null, [], null);
    }

    private static ExecutionMode? ParseExecutionMode(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? mode = null;
        if (root.TryGetProperty("executionMode", out JsonElement lower) && lower.ValueKind == JsonValueKind.String)
        {
            mode = lower.GetString();
        }
        else if (root.TryGetProperty("ExecutionMode", out JsonElement upper) && upper.ValueKind == JsonValueKind.String)
        {
            mode = upper.GetString();
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        return Enum.TryParse(mode, true, out ExecutionMode parsed) ? parsed : null;
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
        Workflow[]? LegacyWorkflows,
        ExecutionMode? ExecutionMode);
}
