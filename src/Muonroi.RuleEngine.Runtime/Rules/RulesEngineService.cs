using Muonroi.RuleEngine.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Rules;

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
    ICanaryRolloutService? canaryRolloutService = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null,
    RuleGraphParser? graphParser = null,
    IMLog<RulesEngineService>? log = null)
{
    private readonly ReSettings _settings = settings ?? new ReSettings();
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;
    private readonly IRuleSetRuntimeCache? _runtimeCache = runtimeCache;
    private readonly IRuleSetChangeNotifier? _notifier = notifier;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    private readonly IRuleSetDefinitionValidator _validator = validator ?? new RuleSetDefinitionValidator();
    private readonly ICanaryRolloutService? _canaryRolloutService = canaryRolloutService;
    private readonly ISystemExecutionContextAccessor _executionContext =
        executionContextAccessor ??
        serviceProvider?.GetService<ISystemExecutionContextAccessor>() ??
        new SystemExecutionContextAccessor();
    private readonly RuleGraphParser? _graphParser =
        graphParser ??
        serviceProvider?.GetService<RuleGraphParser>();
    private readonly IMLog<RulesEngineService>? _log = log;

    private static readonly ConcurrentDictionary<string, CachedWorkflowDefinition> WorkflowCache =
        new(StringComparer.OrdinalIgnoreCase);
    private const int MaxWorkflowCacheEntries = 2048;
    private static readonly SemaphoreSlim _evictionLock = new(1, 1);

    static RulesEngineService()
    {
        // Register the WorkflowCache size observable gauge once at class initialization.
        WorkflowCacheTelemetry.RegisterCacheSizeProvider(() => WorkflowCache.Count);
    }

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, IReadOnlyList<Type>>> ReflectionRuleCache =
        new();

    private static readonly object ReflectionRuleCacheLock = new();
    private static int _knownAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;

    /// <summary>Saves a ruleset definition for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="json">Ruleset JSON payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveRuleSetAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        _validator.Validate(workflowName, json).ThrowIfInvalid();
        await store.SaveAsync(workflowName, json, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Saved, null, cancellationToken);
    }

    /// <summary>Sets the active ruleset version for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Version number to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        await store.SetActiveVersionAsync(workflowName, version, cancellationToken);
        await NotifyRuleChangedAsync(workflowName, RuleSetChangeTypes.Activated, version, cancellationToken);
    }

    /// <summary>Validates a ruleset definition without saving it.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="json">Ruleset JSON payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public Task<RuleSetValidationResult> ValidateRuleSetAsync(string workflowName, string json,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnsureRuleEngineFeature();
        RuleSetValidationResult result = _validator.Validate(workflowName, json);
        return Task.FromResult(result);
    }

    /// <summary>Gets a ruleset definition by workflow and optional version.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Optional version; when null, uses the active version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ruleset JSON or <c>null</c> if missing.</returns>
    public async Task<string?> GetRuleSetAsync(string workflowName, int? version = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetAsync(workflowName, version, cancellationToken);
    }

    /// <summary>Lists available versions for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of versions.</returns>
    public async Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetVersionsAsync(workflowName, cancellationToken);
    }

    /// <summary>Lists version details (version, status, isActive, createdAt) for a workflow with pagination.</summary>
    public async Task<IReadOnlyList<(int Version, string Status, bool IsActive, DateTimeOffset CreatedAt)>> GetVersionDetailsAsync(
        string workflowName, int limit = 10, int offset = 0, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetVersionDetailsAsync(workflowName, limit, offset, cancellationToken);
    }

    /// <summary>Gets the active version for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active version or <c>null</c> if unknown.</returns>
    public async Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetActiveVersionAsync(workflowName, cancellationToken);
    }

    /// <summary>Lists workflows visible to the current tenant scope.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workflow names.</returns>
    public async Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        return await store.GetWorkflowsAsync(cancellationToken);
    }

    /// <summary>Executes a workflow and returns the full orchestration result.</summary>
    /// <typeparam name="TContext">Type of the execution context.</typeparam>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The orchestrator result with facts and errors.</returns>
    public Task<OrchestratorResult> ExecuteWithResultAsync<TContext>(
        string workflowName,
        TContext context,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResultAsync(workflowName, context, initialFacts: null, cancellationToken);
    }

    /// <summary>Executes a workflow with detailed result and optional initial facts seeded into the FactBag.</summary>
    /// <typeparam name="TContext">Type of the execution context.</typeparam>
    /// <param name="workflowName">The workflow name to execute.</param>
    /// <param name="context">The context carrying data for rule evaluation.</param>
    /// <param name="initialFacts">Optional initial facts to seed into the FactBag before rule execution.
    /// Useful for dry-run where Liquid/FEEL templates need access to the input data (e.g., command.header).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed execution result.</returns>
    public async Task<OrchestratorResult> ExecuteWithResultAsync<TContext>(
        string workflowName,
        TContext context,
        IReadOnlyDictionary<string, object?>? initialFacts,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        (string tenantId, string? json) = await LoadWorkflowJsonAsync(workflowName, cancellationToken);
        if (json is null)
        {
            return OrchestratorResult.Success(
                ExecutionMode.AllOrNothing,
                new FactBag(),
                new Dictionary<string, RuleResult>(StringComparer.OrdinalIgnoreCase));
        }

        if (_graphParser is not null && _graphParser.CanParse(json))
        {
            _log?.Info(
                "Executing workflow '{WorkflowName}' using flow-graph dispatch for context '{ContextType}'.",
                workflowName,
                typeof(TContext).FullName ?? typeof(TContext).Name);
            return await ExecuteFlowGraphWithResultAsync(workflowName, json, context, initialFacts, cancellationToken);
        }

        _log?.Info(
            "Executing workflow '{WorkflowName}' using legacy code-workflow dispatch for context '{ContextType}'.",
            workflowName,
            typeof(TContext).FullName ?? typeof(TContext).Name);
        CachedWorkflowDefinition definition = GetOrCreateWorkflowDefinition(tenantId, workflowName, json);
        string[]? codes = definition.RuleCodes;

        if (codes is not null)
        {
            return await ExecuteCodeWorkflowWithResultAsync(
                codes,
                context,
                definition.ExecutionMode,
                cancellationToken);
        }

        Workflow[] workflows = definition.LegacyWorkflows ?? [];
        FactBag facts = await ExecuteLegacyWorkflowAsync(workflowName, workflows, context);
        return OrchestratorResult.Success(
            definition.ExecutionMode ?? ExecutionMode.AllOrNothing,
            facts,
            new Dictionary<string, RuleResult>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Executes a workflow and returns the output facts.</summary>
    /// <typeparam name="TContext">Type of the execution context.</typeparam>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output fact bag.</returns>
    public async Task<FactBag> ExecuteAsync<TContext>(string workflowName, TContext context,
        CancellationToken cancellationToken = default)
    {
        OrchestratorResult execution = await ExecuteWithResultAsync(workflowName, context, cancellationToken);
        if (execution.IsSuccess)
        {
            return execution.Facts;
        }

        string message = execution.Errors.Count > 0
            ? string.Join("; ", execution.Errors)
            : "Rule orchestration failed.";
        if (execution.CompensationErrors.Count > 0)
        {
            message = $"{message} Compensation: {string.Join("; ", execution.CompensationErrors)}";
        }

        throw new MInternalException(message);
    }

    /// <summary>
    /// Executes a ruleset payload in memory without persisting it.
    /// Supports legacy, code-based, and flow-graph ruleset shapes.
    /// </summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="json">Ruleset JSON payload.</param>
    /// <param name="context">Context payload as JSON.</param>
    /// <param name="contextType">Optional context type name for code/graph rulesets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output fact bag.</returns>
    public async Task<FactBag> DryRunAsync(
        string workflowName,
        string json,
        JsonElement context,
        string? contextType = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRuleEngineFeature();
        _validator.Validate(workflowName, json).ThrowIfInvalid();

        if (_graphParser is not null && _graphParser.CanParse(json))
        {
            if (string.IsNullOrWhiteSpace(contextType))
            {
                throw new MConfigurationException(
                    "Graph-based dry-run requires 'contextType' (assembly-qualified name or full type name).");
            }

            Type resolvedGraphContext = ResolveContextType(contextType);
            object? graphContext = JsonSerializer.Deserialize(context.GetRawText(), resolvedGraphContext, // MBB002-exempt: requires Type-based overload with custom options not available in wrapper
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (graphContext is null)
            {
                throw new MConfigurationException("Dry-run context payload could not be deserialized to the requested contextType.");
            }

            return await ExecuteFlowGraphDynamicAsync(workflowName, json, graphContext, cancellationToken);
        }

        CachedWorkflowDefinition definition = ParseWorkflowDefinition(json);
        if (definition.RuleCodes is not null)
        {
            if (string.IsNullOrWhiteSpace(contextType))
            {
                throw new MConfigurationException(
                    "Code-based ruleset dry-run requires 'contextType' (assembly-qualified name or full type name).");
            }

            Type resolved = ResolveContextType(contextType);
            object? contextValue = JsonSerializer.Deserialize(context.GetRawText(), resolved, // MBB002-exempt: requires Type-based overload with custom options not available in wrapper
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (contextValue is null)
            {
                throw new MConfigurationException("Dry-run context payload could not be deserialized to the requested contextType.");
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

    private async Task<(string TenantId, string? Json)> LoadWorkflowJsonAsync(
        string workflowName,
        CancellationToken cancellationToken)
    {
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

        return (tenantId, json);
    }

    private async Task<FactBag> ExecuteCodeWorkflowAsync<TContext>(
        IReadOnlyList<string> codes,
        TContext context,
        ExecutionMode? executionMode,
        CancellationToken cancellationToken)
    {
        OrchestratorResult execution = await ExecuteCodeWorkflowWithResultAsync(
            codes,
            context,
            executionMode,
            cancellationToken);

        if (execution.IsSuccess)
        {
            return execution.Facts;
        }

        string message = execution.Errors.Count > 0
            ? string.Join("; ", execution.Errors)
            : "Rule orchestration failed.";
        if (execution.CompensationErrors.Count > 0)
        {
            message = $"{message} Compensation: {string.Join("; ", execution.CompensationErrors)}";
        }

        throw new MInternalException(message);
    }

    private async Task<OrchestratorResult> ExecuteCodeWorkflowWithResultAsync<TContext>(
        IReadOnlyList<string> codes,
        TContext context,
        ExecutionMode? executionMode,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<IRule<TContext>>> rulesByCode = ResolveRulesByCode<TContext>(codes);
        if (rulesByCode.Count == 0)
        {
            throw new MConfigurationException("Ruleset uses code-based workflow but no rule implementations were discovered.");
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
            throw new MConfigurationException(
                $"Ruleset references unknown rule code(s): {string.Join(", ", missingCodes.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }

        if (ambiguousCodes.Count > 0)
        {
            string detail = string.Join(" | ", ambiguousCodes.Select(kv => $"{kv.Key} => [{string.Join(", ", kv.Value)}]"));
            throw new RuleEngineAmbiguousCodeException($"Ruleset contains ambiguous rule code mappings: {detail}.");
        }

        IEnumerable<IHookHandler<TContext>> hooks = _serviceProvider?.GetServices<IHookHandler<TContext>>() ?? [];
        IEnumerable<IRuleEventListener<TContext>> listeners =
            _serviceProvider?.GetServices<IRuleEventListener<TContext>>() ?? [];
        IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>? logger =
            _serviceProvider?.GetService<IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>>();
        ITenantQuotaTracker? quotaTracker = _serviceProvider?.GetService<ITenantQuotaTracker>();
        IRuleExecutionTracer? tracer = _serviceProvider?.GetService<IRuleExecutionTracer>();
        Muonroi.RuleEngine.Core.RuleOrchestrator<TContext> orchestrator =
            new(resolvedRules, hooks, logger, listeners, quotaTracker, tracer, _executionContext);
        return await orchestrator.ExecuteWithResultAsync(
            context,
            executionMode ?? ExecutionMode.AllOrNothing,
            cancellationToken: cancellationToken);
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
        if (closed.Invoke(this, [codes, context, executionMode, cancellationToken]) is not Task<FactBag> invoke)
        {
            throw new MInternalException("Unable to invoke code-based dry-run bridge.");
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
            throw new MConfigurationException($"Dry-run context type mismatch. Expected '{typeof(TContext).FullName}'.");
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
        if (closed.Invoke(this, [workflowName, workflows, context]) is not Task<FactBag> invoke)
        {
            throw new MInternalException("Unable to invoke legacy dry-run bridge.");
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
            throw new MConfigurationException($"Legacy dry-run context type mismatch. Expected '{typeof(TContext).FullName}'.");
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

        throw new MConfigurationException($"Cannot resolve contextType '{contextTypeName}'.");
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
        long startTicks = Stopwatch.GetTimestamp();
        string tenantId = ResolveTenantId();
        if (_runtimeCache is not null)
        {
            await _runtimeCache.InvalidateAsync(tenantId, workflowName, cancellationToken);
        }
        WorkflowCache.TryRemove(BuildWorkflowCacheKey(tenantId, workflowName), out _);
        double elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
        WorkflowCacheTelemetry.RecordHotReloadLag(elapsedMs);

        if (_notifier is not null)
        {
            await _notifier.PublishAsync(
                new RuleSetChangeEvent(tenantId, workflowName, changeType, version, DateTimeOffset.UtcNow),
                cancellationToken);
        }
    }

    private string ResolveTenantId()
    {
        string? tenantId = _executionContext.Get().TenantId;
        return string.IsNullOrWhiteSpace(tenantId)
            ? "default"
            : tenantId;
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
            // Update access time on cache hit to maintain LRU ordering (per D-01)
            WorkflowCache[cacheKey] = cached with { LastAccessedUtc = DateTime.UtcNow };
            WorkflowCacheTelemetry.RecordHit(tenantId);
            return cached;
        }

        CachedWorkflowDefinition parsed = ParseWorkflowDefinition(json) with { LastAccessedUtc = DateTime.UtcNow };
        WorkflowCache[cacheKey] = parsed;
        WorkflowCacheTelemetry.RecordMiss(tenantId);

        if (WorkflowCache.Count > MaxWorkflowCacheEntries)
        {
            EvictOldestEntries();
        }

        return parsed;
    }

    /// <summary>
    /// Evicts the oldest 25% of WorkflowCache entries by LastAccessedUtc.
    /// Uses SemaphoreSlim(1,1) with non-blocking tryAcquire to prevent concurrent eviction stampede (per D-02).
    /// </summary>
    private static void EvictOldestEntries()
    {
        if (!_evictionLock.Wait(0)) return; // Another thread is already evicting — skip
        try
        {
            if (WorkflowCache.Count <= MaxWorkflowCacheEntries) return; // Double-checked after acquiring lock

            string[] toEvict = WorkflowCache.ToArray()
                .OrderBy(kv => kv.Value.LastAccessedUtc)
                .Take(MaxWorkflowCacheEntries / 4) // 512 entries = 25%
                .Select(kv => kv.Key)
                .ToArray();

            foreach (string key in toEvict)
            {
                WorkflowCache.TryRemove(key, out _);
            }

            WorkflowCacheTelemetry.RecordEviction(toEvict.Length);
        }
        finally
        {
            _evictionLock.Release();
        }
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

    // =========================================================================
    // Graph-based dispatch
    // =========================================================================

    private async Task<FactBag> ExecuteFlowGraphAsync<TContext>(
        string workflowName,
        string graphJson,
        TContext context,
        CancellationToken cancellationToken)
    {
        OrchestratorResult execution = await ExecuteFlowGraphWithResultAsync(
            workflowName,
            graphJson,
            context,
            initialFacts: null,
            cancellationToken);

        if (execution.IsSuccess)
        {
            return execution.Facts;
        }

        string message = execution.Errors.Count > 0
            ? string.Join("; ", execution.Errors)
            : "Flow graph execution failed.";
        if (execution.CompensationErrors.Count > 0)
        {
            message = $"{message} Compensation: {string.Join("; ", execution.CompensationErrors)}";
        }

        throw new MInternalException(message);
    }

    private async Task<OrchestratorResult> ExecuteFlowGraphWithResultAsync<TContext>(
        string workflowName,
        string graphJson,
        TContext context,
        IReadOnlyDictionary<string, object?>? initialFacts = null,
        CancellationToken cancellationToken = default)
    {
        _ = workflowName;
        IReadOnlyList<RuleGraphEntry> entries = _graphParser!.Parse(graphJson);

        List<IRule<TContext>> rules = [];
        foreach (RuleGraphEntry entry in entries)
        {
            IRule<TContext>? rule = ResolveRuleEntry<TContext>(entry);
            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        IEnumerable<IHookHandler<TContext>> hooks = _serviceProvider?.GetServices<IHookHandler<TContext>>() ?? [];
        IEnumerable<IRuleEventListener<TContext>> listeners =
            _serviceProvider?.GetServices<IRuleEventListener<TContext>>() ?? [];
        IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>? logger =
            _serviceProvider?.GetService<IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<TContext>>>();
        ITenantQuotaTracker? quotaTracker = _serviceProvider?.GetService<ITenantQuotaTracker>();
        IRuleExecutionTracer? tracer = _serviceProvider?.GetService<IRuleExecutionTracer>();

        Muonroi.RuleEngine.Core.RuleOrchestrator<TContext> orchestrator =
            new(rules, hooks, logger, listeners, quotaTracker, tracer, _executionContext);

        return await orchestrator.ExecuteWithResultAsync(
            context,
            ExecutionMode.AllOrNothing,
            initialFacts: initialFacts,
            cancellationToken: cancellationToken);
    }

    private IRule<TContext>? ResolveRuleEntry<TContext>(RuleGraphEntry entry)
    {
        // Type A: compiled rule — DI lookup by code
        if (!string.IsNullOrEmpty(entry.RuleCode) && _serviceProvider is not null)
        {
            IRule<TContext>? compiled = _serviceProvider
                .GetServices<IRule<TContext>>()
                .FirstOrDefault(r => string.Equals(r.Code, entry.RuleCode, StringComparison.OrdinalIgnoreCase));
            if (compiled is not null)
            {
                _log?.Info(
                    "Resolved node '{NodeId}' as compiled rule '{RuleCode}'.",
                    entry.NodeId,
                    entry.RuleCode);
                return WrapRuleEntry(compiled, entry);
            }

            // ruleCode present but not found in DI — log warning and skip gracefully
            // (allows soft-delete of rules without breaking the whole flow)
        }

        IContextProjector<TContext> projector = GetProjector<TContext>();

        // Type B-1a: JavaScript condition
        if (!string.IsNullOrEmpty(entry.JavaScriptExpression))
        {
            IMLog<JavaScriptRuleAdapter<TContext>>? jsLog =
                _serviceProvider?.GetService<IMLog<JavaScriptRuleAdapter<TContext>>>();
            if (jsLog is null)
            {
                return null;
            }

            _log?.Info("Resolved node '{NodeId}' as JavaScript adapter.", entry.NodeId);
            return WrapRuleEntry(
                new JavaScriptRuleAdapter<TContext>(
                    entry.NodeId,
                    entry.JavaScriptExpression,
                    entry.OutputFields,
                    projector,
                    jsLog),
                entry);
        }

        // Type B-1b: FEEL condition
        if (!string.IsNullOrEmpty(entry.FeelExpression))
        {
            IMLog<FeelRuleAdapter<TContext>>? feelLog =
                _serviceProvider?.GetService<IMLog<FeelRuleAdapter<TContext>>>();
            if (feelLog is null)
            {
                return null;
            }

            _log?.Info("Resolved node '{NodeId}' as FEEL adapter.", entry.NodeId);
            return WrapRuleEntry(
                new FeelRuleAdapter<TContext>(
                    entry.NodeId,
                    entry.FeelExpression,
                    entry.OutputFields,
                    projector,
                    feelLog),
                entry);
        }

        // Type B-2: Liquid/Scriban action
        if (!string.IsNullOrEmpty(entry.LiquidTemplate))
        {
            IMLog<LiquidRuleAdapter<TContext>>? liquidLog =
                _serviceProvider?.GetService<IMLog<LiquidRuleAdapter<TContext>>>();
            IMJsonSerializeService jsonSvc =
                _serviceProvider?.GetService<IMJsonSerializeService>()
                ?? new Muonroi.Core.Abstractions.SeedWorks.MJsonSerializeService();
            if (liquidLog is null)
            {
                return null;
            }

            IEnumerable<IScribanFunctionProvider>? functionProviders =
                _serviceProvider?.GetServices<IScribanFunctionProvider>();

            _log?.Info("Resolved node '{NodeId}' as Liquid/Scriban adapter.", entry.NodeId);
            return WrapRuleEntry(
                new LiquidRuleAdapter<TContext>(
                    entry.NodeId,
                    entry.LiquidTemplate,
                    entry.LiquidOutputFormat,
                    entry.LiquidOutputKey ?? "liquidOutput",
                    projector,
                    jsonSvc,
                    liquidLog,
                    functionProviders),
                entry);
        }

        // Type B-3: Decision Table
        if (!string.IsNullOrEmpty(entry.DecisionTableId) && _serviceProvider is not null)
        {
            IDecisionTableStore? dtStore = _serviceProvider.GetService<IDecisionTableStore>();
            IDecisionTableExecutor? dtExecutor = _serviceProvider.GetService<IDecisionTableExecutor>();
            IMLog<DecisionTableRuleAdapter<TContext>>? dtLog =
                _serviceProvider.GetService<IMLog<DecisionTableRuleAdapter<TContext>>>();

            if (dtStore is null || dtExecutor is null || dtLog is null)
            {
                return null;
            }

            _log?.Info("Resolved node '{NodeId}' as Decision Table adapter.", entry.NodeId);
            return WrapRuleEntry(
                new DecisionTableRuleAdapter<TContext>(
                    entry.NodeId,
                    entry.DecisionTableId,
                    dtStore,
                    dtExecutor,
                    projector,
                    dtLog,
                    entry.FailOnNoMatch),
                entry);
        }

        // Type B-4: Sub Flow
        if (!string.IsNullOrEmpty(entry.SubFlowCode))
        {
            IMLog<SubFlowRuleAdapter<TContext>>? subLog =
                _serviceProvider?.GetService<IMLog<SubFlowRuleAdapter<TContext>>>();
            if (subLog is null)
            {
                return null;
            }

            _log?.Info(
                "Resolved node '{NodeId}' as Sub Flow adapter targeting '{SubFlowCode}'.",
                entry.NodeId,
                entry.SubFlowCode);
            return new SubFlowRuleAdapter<TContext>(
                entry.NodeId,
                entry.SubFlowCode,
                entry.InputMappings,
                entry.OutputMappings,
                this,
                projector,
                subLog) is { } subFlowRule
                ? WrapRuleEntry(subFlowRule, entry)
                : null;
        }

        // Type B-5: Connector
        if (!string.IsNullOrEmpty(entry.ConnectorType) && _serviceProvider is not null)
        {
            Muonroi.Integration.Abstractions.IConnectorRegistry? registry =
                _serviceProvider.GetService<Muonroi.Integration.Abstractions.IConnectorRegistry>();
            Muonroi.Integration.Abstractions.IConnectorCredentialStore? credStore =
                _serviceProvider.GetService<Muonroi.Integration.Abstractions.IConnectorCredentialStore>();
            IMLog<ConnectorRuleAdapter<TContext>>? connLog =
                _serviceProvider.GetService<IMLog<ConnectorRuleAdapter<TContext>>>();

            if (registry is not null && connLog is not null)
            {
                string? tenantId = _executionContext?.Get()?.TenantId;
                _log?.Info("Resolved node '{NodeId}' as Connector adapter (type: {ConnectorType}).", entry.NodeId, entry.ConnectorType);
                return WrapRuleEntry(
                    new ConnectorRuleAdapter<TContext>(
                        entry.NodeId,
                        entry.ConnectorType,
                        entry.ConnectorConfig,
                        entry.CredentialId,
                        registry,
                        credStore,
                        projector,
                        connLog,
                        tenantId),
                    entry);
            }
        }

        // Type B-6: Action with output fields only (no expression)
        // These nodes only compute FEEL output fields without a boolean gate.
        // We create a FeelRuleAdapter with a "true" expression so it always passes
        // and the output fields are evaluated.
        if (entry.OutputFields is { Count: > 0 })
        {
            IMLog<FeelRuleAdapter<TContext>>? feelLog =
                _serviceProvider?.GetService<IMLog<FeelRuleAdapter<TContext>>>();
            if (feelLog is not null)
            {
                _log?.Info("Resolved node '{NodeId}' as action-only FEEL adapter (output fields only).", entry.NodeId);
                return WrapRuleEntry(
                    new FeelRuleAdapter<TContext>(
                        entry.NodeId,
                        "true",
                        entry.OutputFields,
                        projector,
                        feelLog),
                    entry);
            }
        }

        // Skip trigger/end/unknown nodes
        return null;
    }

    private IContextProjector<TContext> GetProjector<TContext>()
    {
        return _serviceProvider?.GetService<IContextProjector<TContext>>()
            ?? new ReflectionContextProjector<TContext>();
    }

    /// <summary>
    /// Executes a child workflow identified by <paramref name="workflowCode"/> using
    /// fact-based context (no typed TContext required). Used by sub-flow nodes.
    /// </summary>
    public async Task<SubFlowExecutionResult> ExecuteSubFlowAsync(
        string workflowCode,
        FactBag inputFacts,
        CancellationToken ct = default)
    {
        // Guard: detect circular sub-flow references
        using IDisposable _ = SubFlowCallStack.Push(workflowCode);

        string? graphJson = await store.GetAsync(workflowCode, cancellationToken: ct);
        if (graphJson is null)
        {
            return new SubFlowExecutionResult
            {
                IsSuccess = false,
                Errors    = [$"SubFlow workflow '{workflowCode}' not found or has no active version."]
            };
        }

        if (_graphParser is null || !_graphParser.CanParse(graphJson))
        {
            return new SubFlowExecutionResult
            {
                IsSuccess = false,
                Errors    = [$"SubFlow workflow '{workflowCode}' is not a flow graph — sub-flow requires a graph-based ruleset."]
            };
        }

        IReadOnlyList<RuleGraphEntry> entries = _graphParser.Parse(graphJson);

        List<IRule<FactBagRuleContext>> rules = [];
        foreach (RuleGraphEntry entry in entries)
        {
            IRule<FactBagRuleContext>? rule = ResolveRuleEntryAsFactBag(entry);
            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        IEnumerable<IHookHandler<FactBagRuleContext>> hooks =
            _serviceProvider?.GetServices<IHookHandler<FactBagRuleContext>>() ?? [];
        IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<FactBagRuleContext>>? logger =
            _serviceProvider?.GetService<IMLog<Muonroi.RuleEngine.Core.RuleOrchestrator<FactBagRuleContext>>>();
        IRuleExecutionTracer? tracer = _serviceProvider?.GetService<IRuleExecutionTracer>();

        Muonroi.RuleEngine.Core.RuleOrchestrator<FactBagRuleContext> orchestrator =
            new(rules, hooks, logger, [], null, tracer, _executionContext);

        FactBagRuleContext ctx = new(inputFacts);
        OrchestratorResult execution = await orchestrator.ExecuteWithResultAsync(
            ctx, ExecutionMode.AllOrNothing, cancellationToken: ct);

        if (!execution.IsSuccess)
        {
            return new SubFlowExecutionResult
            {
                IsSuccess = false,
                Errors    = execution.Errors
            };
        }

        return new SubFlowExecutionResult
        {
            IsSuccess   = true,
            OutputFacts = execution.Facts
        };
    }

    private IRule<FactBagRuleContext>? ResolveRuleEntryAsFactBag(RuleGraphEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.RuleCode))
        {
            IRule<FactBagRuleContext>? compiled = TryResolveCompiledRuleAsFactBag(entry);
            if (compiled is not null)
            {
                return compiled;
            }
        }

        IContextProjector<FactBagRuleContext> projector = GetProjector<FactBagRuleContext>();

        // Type B-1a: JavaScript condition
        if (!string.IsNullOrEmpty(entry.JavaScriptExpression))
        {
            IMLog<JavaScriptRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider?.GetService<IMLog<JavaScriptRuleAdapter<FactBagRuleContext>>>();
            if (log is null) return null;
            _log?.Info("Resolved node '{NodeId}' as JavaScript adapter inside sub-flow.", entry.NodeId);
            return WrapRuleEntry(
                new JavaScriptRuleAdapter<FactBagRuleContext>(
                    entry.NodeId,
                    entry.JavaScriptExpression,
                    entry.OutputFields,
                    projector,
                    log),
                entry);
        }

        // Type B-1b: FEEL condition
        if (!string.IsNullOrEmpty(entry.FeelExpression))
        {
            IMLog<FeelRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider?.GetService<IMLog<FeelRuleAdapter<FactBagRuleContext>>>();
            if (log is null) return null;
            _log?.Info("Resolved node '{NodeId}' as FEEL adapter inside sub-flow.", entry.NodeId);
            return WrapRuleEntry(
                new FeelRuleAdapter<FactBagRuleContext>(
                    entry.NodeId,
                    entry.FeelExpression,
                    entry.OutputFields,
                    projector,
                    log),
                entry);
        }

        // Type B-2: Liquid/Scriban action
        if (!string.IsNullOrEmpty(entry.LiquidTemplate))
        {
            IMLog<LiquidRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider?.GetService<IMLog<LiquidRuleAdapter<FactBagRuleContext>>>();
            IMJsonSerializeService jsonSvc =
                _serviceProvider?.GetService<IMJsonSerializeService>()
                ?? new Muonroi.Core.Abstractions.SeedWorks.MJsonSerializeService();
            if (log is null) return null;

            IEnumerable<IScribanFunctionProvider>? functionProviders =
                _serviceProvider?.GetServices<IScribanFunctionProvider>();

            _log?.Info("Resolved node '{NodeId}' as Liquid/Scriban adapter inside sub-flow.", entry.NodeId);
            return WrapRuleEntry(
                new LiquidRuleAdapter<FactBagRuleContext>(
                    entry.NodeId,
                    entry.LiquidTemplate,
                    entry.LiquidOutputFormat,
                    entry.LiquidOutputKey ?? "liquidOutput",
                    projector,
                    jsonSvc,
                    log,
                    functionProviders),
                entry);
        }

        // Type B-3: Decision Table
        if (!string.IsNullOrEmpty(entry.DecisionTableId) && _serviceProvider is not null)
        {
            IDecisionTableStore? dtStore = _serviceProvider.GetService<IDecisionTableStore>();
            IDecisionTableExecutor? dtExecutor = _serviceProvider.GetService<IDecisionTableExecutor>();
            IMLog<DecisionTableRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider.GetService<IMLog<DecisionTableRuleAdapter<FactBagRuleContext>>>();
            if (dtStore is null || dtExecutor is null || log is null) return null;
            _log?.Info("Resolved node '{NodeId}' as Decision Table adapter inside sub-flow.", entry.NodeId);
            return WrapRuleEntry(
                new DecisionTableRuleAdapter<FactBagRuleContext>(
                    entry.NodeId,
                    entry.DecisionTableId,
                    dtStore,
                    dtExecutor,
                    projector,
                    log,
                    entry.FailOnNoMatch),
                entry);
        }

        // Type B-4: Sub Flow
        if (!string.IsNullOrEmpty(entry.SubFlowCode))
        {
            IMLog<SubFlowRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider?.GetService<IMLog<SubFlowRuleAdapter<FactBagRuleContext>>>();
            if (log is null) return null;
            _log?.Info(
                "Resolved node '{NodeId}' as Sub Flow adapter targeting '{SubFlowCode}'.",
                entry.NodeId,
                entry.SubFlowCode);
            return new SubFlowRuleAdapter<FactBagRuleContext>(
                entry.NodeId,
                entry.SubFlowCode,
                entry.InputMappings,
                entry.OutputMappings,
                this,
                projector,
                log) is { } subFlowRule
                ? WrapRuleEntry(subFlowRule, entry)
                : null;
        }

        // Type B-5: Connector
        if (!string.IsNullOrEmpty(entry.ConnectorType) && _serviceProvider is not null)
        {
            Muonroi.Integration.Abstractions.IConnectorRegistry? registry =
                _serviceProvider.GetService<Muonroi.Integration.Abstractions.IConnectorRegistry>();
            Muonroi.Integration.Abstractions.IConnectorCredentialStore? credStore =
                _serviceProvider.GetService<Muonroi.Integration.Abstractions.IConnectorCredentialStore>();
            IMLog<ConnectorRuleAdapter<FactBagRuleContext>>? log =
                _serviceProvider.GetService<IMLog<ConnectorRuleAdapter<FactBagRuleContext>>>();

            if (registry is not null && log is not null)
            {
                string? tenantId = _executionContext?.Get()?.TenantId;
                _log?.Info("Resolved node '{NodeId}' as Connector adapter inside sub-flow.", entry.NodeId);
                return WrapRuleEntry(
                    new ConnectorRuleAdapter<FactBagRuleContext>(
                        entry.NodeId,
                        entry.ConnectorType,
                        entry.ConnectorConfig,
                        entry.CredentialId,
                        registry,
                        credStore,
                        projector,
                        log,
                        tenantId),
                    entry);
            }
        }

        return null;
    }

    private async Task<FactBag> ExecuteFlowGraphDynamicAsync(
        string workflowName,
        string graphJson,
        object context,
        CancellationToken cancellationToken)
    {
        MethodInfo? bridge = GetType().GetMethod(
            nameof(ExecuteFlowGraphBridgeAsync),
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (bridge is null)
        {
            throw new MissingMethodException(nameof(ExecuteFlowGraphBridgeAsync));
        }

        MethodInfo closed = bridge.MakeGenericMethod(context.GetType());
        if (closed.Invoke(this, [workflowName, graphJson, context, cancellationToken]) is not Task<FactBag> invoke)
        {
            throw new MInternalException("Unable to invoke flow-graph execution bridge.");
        }

        return await invoke;
    }

    private Task<FactBag> ExecuteFlowGraphBridgeAsync<TContext>(
        string workflowName,
        string graphJson,
        object context,
        CancellationToken cancellationToken)
    {
        if (context is not TContext typed)
        {
            throw new MConfigurationException($"Flow-graph context type mismatch. Expected '{typeof(TContext).FullName}'.");
        }

        return ExecuteFlowGraphAsync(workflowName, graphJson, typed, cancellationToken);
    }

    private IRule<FactBagRuleContext>? TryResolveCompiledRuleAsFactBag(RuleGraphEntry entry)
    {
        if (_serviceProvider is null || string.IsNullOrWhiteSpace(entry.RuleCode))
        {
            return null;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type candidateType in GetLoadableTypes(assembly))
            {
                if (!candidateType.IsClass || candidateType.IsAbstract || candidateType.ContainsGenericParameters)
                {
                    continue;
                }

                Type? ruleInterface = candidateType.GetInterfaces()
                    .FirstOrDefault(iface =>
                        iface.IsGenericType &&
                        iface.GetGenericTypeDefinition() == typeof(IRule<>));
                if (ruleInterface is null)
                {
                    continue;
                }

                object? candidate = TryCreateRuleInstance(candidateType);
                if (candidate is null)
                {
                    continue;
                }

                string? code;
                try
                {
                    object? codeValue = candidateType.GetProperty(nameof(IRule<FactBagRuleContext>.Code))?.GetValue(candidate);
                    code = codeValue as string;
                }
                catch
                {
                    continue;
                }

                if (code is null ||
                    !string.Equals(code, entry.RuleCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type childContextType = ruleInterface.GetGenericArguments()[0];
                Type factoryType = typeof(IContextFactory<>).MakeGenericType(childContextType);
                object? factory = _serviceProvider.GetService(factoryType);
                if (factory is null)
                {
                    continue;
                }

                Type adapterType = typeof(ContextAdaptedRule<>).MakeGenericType(childContextType);
                object? adapted = Activator.CreateInstance(adapterType, candidate, factory);
                if (adapted is IRule<FactBagRuleContext> typed)
                {
                    return WrapRuleEntry(typed, entry);
                }
            }
        }

        return null;
    }

    private static IRule<TContext> WrapRuleEntry<TContext>(IRule<TContext> rule, RuleGraphEntry entry)
    {
        return new GraphRuleDispatchAdapter<TContext>(
            new RuleEntryOverrideAdapter<TContext>(rule, entry.Order, entry.DependsOn),
            entry);
    }

    private sealed record CachedWorkflowDefinition(
        string Json,
        string[]? RuleCodes,
        Workflow[]? LegacyWorkflows,
        ExecutionMode? ExecutionMode,
        DateTime LastAccessedUtc = default);
}
