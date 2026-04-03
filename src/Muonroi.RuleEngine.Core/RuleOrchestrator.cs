using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Executes rules in dependency order and triggers hook handlers.
/// </summary>
/// <typeparam name="TContext">The execution context type.</typeparam>
public sealed class RuleOrchestrator<TContext>(
    IEnumerable<IRule<TContext>> rules,
    IEnumerable<IHookHandler<TContext>> hooks,
    IMLog<RuleOrchestrator<TContext>>? logger = null,
    IMJsonSerializeService? jsonSerializeService = null,
    IEnumerable<IRuleEventListener<TContext>>? listeners = null,
    ITenantQuotaTracker? quotaTracker = null,
    IRuleExecutionTracer? tracer = null,
    ISystemExecutionContextAccessor? contextAccessor = null,
    IMTraceContext? traceContext = null)
{

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleOrchestrator{TContext}"/> class.
    /// </summary>
    /// <param name="rules">The rules to orchestrate.</param>
    /// <param name="hooks">The hook handlers.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="listeners">The rule event listeners.</param>
    /// <param name="quotaTracker">The tenant quota tracker.</param>
    /// <param name="tracer">The rule execution tracer.</param>
    /// <param name="contextAccessor">The system execution context accessor.</param>
    /// <param name="traceContext">The trace context.</param>
    public RuleOrchestrator(
        IEnumerable<IRule<TContext>> rules,
        IEnumerable<IHookHandler<TContext>> hooks,
        IMLog<RuleOrchestrator<TContext>>? logger,
        IEnumerable<IRuleEventListener<TContext>>? listeners,
        ITenantQuotaTracker? quotaTracker = null,
        IRuleExecutionTracer? tracer = null,
        ISystemExecutionContextAccessor? contextAccessor = null,
        IMTraceContext? traceContext = null)
        : this(rules, hooks, logger, null, listeners, quotaTracker, tracer, contextAccessor, traceContext)
    {
    }

    private readonly IReadOnlyList<IRule<TContext>> _rules = [.. Order(rules)];

    private readonly IEnumerable<IRuleEventListener<TContext>> _listeners = listeners ?? [];
    private readonly IMJsonSerializeService _jsonSerializeService =
        jsonSerializeService ?? new MJsonSerializeService();
    private readonly ITenantQuotaTracker? _quotaTracker = quotaTracker;
    private readonly IRuleExecutionTracer? _tracer = tracer;
    private readonly ISystemExecutionContextAccessor? _contextAccessor = contextAccessor;
    private readonly IMTraceContext? _traceContext = traceContext;

    /// <summary>
    /// Executes the orchestration pipeline.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="filterPoint">Optional hook point filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{FactBag}"/> representing the asynchronous operation.</returns>
    public async Task<FactBag> ExecuteAsync(TContext context, HookPoint? filterPoint = null, CancellationToken cancellationToken = default)
    {
        FactBag facts = new();
        bool hasFilter = filterPoint.HasValue;
        string? tenantId = ResolveTenantId(context);
        bool concurrentReserved = false;
        int executedRuleCount = 0;

        if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId))
        {
            bool allowedConcurrent = await _quotaTracker.CheckQuotaAsync(
                tenantId,
                QuotaType.ConcurrentExecutions,
                1,
                cancellationToken);
            if (!allowedConcurrent)
            {
                throw new QuotaExceededException($"Concurrent execution quota exceeded for tenant '{tenantId}'.");
            }

            await _quotaTracker.IncrementUsageAsync(tenantId, QuotaType.ConcurrentExecutions, 1, cancellationToken);
            concurrentReserved = true;
        }

        try
        {
            foreach (IRule<TContext> rule in _rules)
            {
                if (hasFilter && rule.HookPoint != filterPoint!.Value)
                {
                    continue;
                }

                if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId))
                {
                    bool canEvaluate = await _quotaTracker.CheckQuotaAsync(
                        tenantId,
                        QuotaType.RuleEvaluationsPerSecond,
                        1,
                        cancellationToken);
                    if (!canEvaluate)
                    {
                        throw new QuotaExceededException($"Rule evaluation quota exceeded for tenant '{tenantId}'.");
                    }

                    await _quotaTracker.IncrementUsageAsync(
                        tenantId,
                        QuotaType.RuleEvaluationsPerSecond,
                        1,
                        cancellationToken);
                }

                logger?.Info("Executing rule {Rule} with facts {@Facts}", rule.Name, facts);

                string version = rule.GetType().Assembly.GetName().Version?.ToString() ?? "unknown";
                string? traceTenantId = Activity.Current?.GetTagItem("tenant.id") as string;
                TagList tags = new() { { "rule.id", rule.Code }, { "rule.set.version", version } };
                if (!string.IsNullOrEmpty(traceTenantId))
                {
                    tags.Add("tenant.id", traceTenantId);
                }

                RuleEngineTelemetry.RulesMatched.Add(1, tags);

                await NotifyMatched(rule, context, facts, cancellationToken);

                // ── Diagnostics ──────────────────────────────────────────────────
                using var ruleNode = _traceContext?.Current?.BeginNode(rule.Code, MTraceNodeType.Rule);

                Dictionary<string, object?> beforeFacts = new(facts.AsReadOnly());
                await TraceRuleAsync(
                    rule,
                    context,
                    facts,
                    beforeFacts,
                    RuleTracePhase.BeforeEval,
                    elapsed: TimeSpan.Zero,
                    isSuccess: true,
                    failureReason: null,
                    exception: null,
                    cancellationToken);

                using Activity? activity = RuleEngineTelemetry.ActivitySource.StartActivity(rule.Name, ActivityKind.Internal);
                activity?.SetTag("rule.id", rule.Code);
                activity?.SetTag("rule.set.version", version);
                if (!string.IsNullOrEmpty(traceTenantId))
                {
                    activity?.SetTag("tenant.id", traceTenantId);
                }

                Stopwatch sw = Stopwatch.StartNew();
                RuleResult result = RuleResult.Passed();
                try
                {
                    await RunHooks(HookPoint.BeforeRule, rule, RuleResult.Passed(), facts, context, null,
                        cancellationToken);

                    result = await rule.EvaluateAsync(context, facts, cancellationToken);
                    await TraceRuleAsync(
                        rule,
                        context,
                        facts,
                        beforeFacts,
                        RuleTracePhase.AfterEval,
                        elapsed: sw.Elapsed,
                        isSuccess: result.IsSuccess,
                        failureReason: result.IsSuccess ? null : BuildRuleFailureMessage(rule, result),
                        exception: null,
                        cancellationToken);

                    if (result.IsSuccess)
                    {
                        await rule.ExecuteAsync(context, cancellationToken);
                        await TraceRuleAsync(
                            rule,
                            context,
                            facts,
                            beforeFacts,
                            RuleTracePhase.AfterExec,
                            elapsed: sw.Elapsed,
                            isSuccess: true,
                            failureReason: null,
                            exception: null,
                            cancellationToken);
                    }

                    sw.Stop();
                    await RunHooks(HookPoint.AfterRule, rule, result, facts, context, sw.Elapsed, cancellationToken);

                    if (!result.IsSuccess)
                    {
                        string errorMessage = result.Errors.Count > 0
                            ? string.Join("; ", result.Errors)
                            : $"Rule {rule.Name} failed.";
                        logger?.Warn("Rule {Rule} failed: {Error}", rule.Name, errorMessage);
                        throw new MInternalException(errorMessage);
                    }

                    executedRuleCount++;
                    logger?.Info("Rule {Rule} succeeded in {Elapsed} ms with facts {@Facts}", rule.Name,
                         sw.ElapsedMilliseconds, facts);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    result = RuleResult.Failure(ex.Message);
                    await RunHooks(HookPoint.Error, rule, result, facts, context, sw.Elapsed, cancellationToken);
                    await TraceRuleAsync(
                        rule,
                        context,
                        facts,
                        beforeFacts,
                        RuleTracePhase.Error,
                        elapsed: sw.Elapsed,
                        isSuccess: false,
                        failureReason: ex.Message,
                        exception: ex,
                        cancellationToken);
                    logger?.Error(ex, "Rule {Rule} failed after {Elapsed} ms", rule.Name, sw.ElapsedMilliseconds);
                    throw;
                }
                finally
                {
                    IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes =
                        DetectChanges(beforeFacts, facts.AsReadOnly());
                    activity?.SetTag("rule.success", result.IsSuccess);
                    activity?.SetTag("rule.duration_ms", sw.Elapsed.TotalMilliseconds);
                    foreach (KeyValuePair<string, (object? OldValue, object? NewValue)> change in changes)
                    {
                        activity?.SetTag($"fact.{change.Key}.old", change.Value.OldValue);
                        activity?.SetTag($"fact.{change.Key}.new", change.Value.NewValue);
                    }

                    RuleEngineTelemetry.RuleEvalDuration.Record(sw.Elapsed.TotalMilliseconds, tags);
                    RuleEngineTelemetry.RulesFired.Add(1, tags);
                    await NotifyFired(rule, result, facts, changes, context, sw.Elapsed, cancellationToken);
                }
            }

            if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId) && executedRuleCount > 0)
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.RuleExecutionsPerDay,
                    executedRuleCount,
                    cancellationToken);
            }

            return facts;
        }
        finally
        {
            if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId) && concurrentReserved)
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.ConcurrentExecutions,
                    -1,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes rules and returns a structured orchestration result using the requested <paramref name="executionMode"/>.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="executionMode">Failure-handling strategy for orchestration.</param>
    /// <param name="filterPoint">Optional hook point filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed execution outcome with per-rule results, errors, and compensation errors.</returns>
    public async Task<OrchestratorResult> ExecuteWithResultAsync(
        TContext context,
        ExecutionMode executionMode = ExecutionMode.AllOrNothing,
        HookPoint? filterPoint = null,
        IReadOnlyDictionary<string, object?>? initialFacts = null,
        CancellationToken cancellationToken = default)
    {
        FactBag facts = new();
        if (initialFacts is not null)
        {
            foreach ((string key, object? value) in initialFacts)
            {
                facts.Set(key, value!);
            }
        }
        Dictionary<string, RuleResult> ruleResults = [];
        List<string> errors = [];
        List<string> compensationErrors = [];
        List<ICompensatableRule<TContext>> executedCompensatableRules = [];

        bool hasFilter = filterPoint.HasValue;
        string? tenantId = ResolveTenantId(context);
        bool concurrentReserved = false;
        int executedRuleCount = 0;

        if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId))
        {
            bool allowedConcurrent = await _quotaTracker.CheckQuotaAsync(
                tenantId,
                QuotaType.ConcurrentExecutions,
                1,
                cancellationToken);
            if (!allowedConcurrent)
            {
                string message = $"Concurrent execution quota exceeded for tenant '{tenantId}'.";
                errors.Add(message);
                return OrchestratorResult.Failure(executionMode, facts, ruleResults, errors, compensationErrors);
            }

            await _quotaTracker.IncrementUsageAsync(tenantId, QuotaType.ConcurrentExecutions, 1, cancellationToken);
            concurrentReserved = true;
        }

        try
        {
            foreach (IRule<TContext> rule in _rules)
            {
                if (hasFilter && rule.HookPoint != filterPoint!.Value)
                {
                    continue;
                }

                if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId))
                {
                    bool canEvaluate = await _quotaTracker.CheckQuotaAsync(
                        tenantId,
                        QuotaType.RuleEvaluationsPerSecond,
                        1,
                        cancellationToken);
                    if (!canEvaluate)
                    {
                        string message = $"Rule evaluation quota exceeded for tenant '{tenantId}'.";
                        RuleResult quotaFailure = RuleResult.Failure(message);
                        ruleResults[rule.Code] = quotaFailure;
                        errors.Add(message);
                        if (executionMode == ExecutionMode.CompensateOnFailure)
                        {
                            await CompensateAsync(executedCompensatableRules, context, facts, compensationErrors,
                                cancellationToken);
                        }

                        if (executionMode != ExecutionMode.BestEffort)
                        {
                            break;
                        }

                        continue;
                    }

                    await _quotaTracker.IncrementUsageAsync(
                        tenantId,
                        QuotaType.RuleEvaluationsPerSecond,
                        1,
                        cancellationToken);
                }

                logger?.Info("Executing rule {Rule} with facts {@Facts}", rule.Name, facts);

                string version = rule.GetType().Assembly.GetName().Version?.ToString() ?? "unknown";
                string? traceTenantId = Activity.Current?.GetTagItem("tenant.id") as string;
                TagList tags = new() { { "rule.id", rule.Code }, { "rule.set.version", version } };
                if (!string.IsNullOrEmpty(traceTenantId))
                {
                    tags.Add("tenant.id", traceTenantId);
                }

                RuleEngineTelemetry.RulesMatched.Add(1, tags);
                await NotifyMatched(rule, context, facts, cancellationToken);

                // ── Diagnostics ──────────────────────────────────────────────────
                using var ruleNode = _traceContext?.Current?.BeginNode(rule.Code, MTraceNodeType.Rule);

                Dictionary<string, object?> beforeFacts = new(facts.AsReadOnly());
                await TraceRuleAsync(
                    rule,
                    context,
                    facts,
                    beforeFacts,
                    RuleTracePhase.BeforeEval,
                    elapsed: TimeSpan.Zero,
                    isSuccess: true,
                    failureReason: null,
                    exception: null,
                    cancellationToken);
                using Activity? activity = RuleEngineTelemetry.ActivitySource.StartActivity(rule.Name, ActivityKind.Internal);
                activity?.SetTag("rule.id", rule.Code);
                activity?.SetTag("rule.set.version", version);
                if (!string.IsNullOrEmpty(traceTenantId))
                {
                    activity?.SetTag("tenant.id", traceTenantId);
                }

                Stopwatch sw = Stopwatch.StartNew();
                RuleResult result = RuleResult.Passed();
                bool stopExecution = false;

                try
                {
                    await RunHooks(HookPoint.BeforeRule, rule, RuleResult.Passed(), facts, context, null,
                        cancellationToken);

                    result = await rule.EvaluateAsync(context, facts, cancellationToken);
                    await TraceRuleAsync(
                        rule,
                        context,
                        facts,
                        beforeFacts,
                        RuleTracePhase.AfterEval,
                        elapsed: sw.Elapsed,
                        isSuccess: result.IsSuccess,
                        failureReason: result.IsSuccess ? null : BuildRuleFailureMessage(rule, result),
                        exception: null,
                        cancellationToken);

                    if (result.IsSuccess)
                    {
                        await rule.ExecuteAsync(context, cancellationToken);
                        await TraceRuleAsync(
                            rule,
                            context,
                            facts,
                            beforeFacts,
                            RuleTracePhase.AfterExec,
                            elapsed: sw.Elapsed,
                            isSuccess: true,
                            failureReason: null,
                            exception: null,
                            cancellationToken);
                        executedRuleCount++;
                        if (executionMode == ExecutionMode.CompensateOnFailure && rule is ICompensatableRule<TContext> compensatableRule)
                        {
                            executedCompensatableRules.Add(compensatableRule);
                        }
                    }

                    sw.Stop();
                    await RunHooks(HookPoint.AfterRule, rule, result, facts, context, sw.Elapsed, cancellationToken);

                    if (!result.IsSuccess)
                    {
                        await RunHooks(HookPoint.Error, rule, result, facts, context, sw.Elapsed, cancellationToken);
                        string errorMessage = BuildRuleFailureMessage(rule, result);
                        await TraceRuleAsync(
                            rule,
                            context,
                            facts,
                            beforeFacts,
                            RuleTracePhase.Error,
                            elapsed: sw.Elapsed,
                            isSuccess: false,
                            failureReason: errorMessage,
                            exception: null,
                            cancellationToken);
                        errors.Add(errorMessage);
                        logger?.Warn("Rule {Rule} failed: {Error}", rule.Name, errorMessage);
                        stopExecution = executionMode != ExecutionMode.BestEffort;
                    }
                    else
                    {
                        logger?.Info("Rule {Rule} succeeded in {Elapsed} ms with facts {@Facts}", rule.Name,
                             sw.ElapsedMilliseconds, facts);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    result = RuleResult.Failure(ex.Message);
                    await RunHooks(HookPoint.Error, rule, result, facts, context, sw.Elapsed, cancellationToken);
                    logger?.Error(ex, "Rule {Rule} failed after {Elapsed} ms", rule.Name, sw.ElapsedMilliseconds);
                    await TraceRuleAsync(
                        rule,
                        context,
                        facts,
                        beforeFacts,
                        RuleTracePhase.Error,
                        elapsed: sw.Elapsed,
                        isSuccess: false,
                        failureReason: ex.Message,
                        exception: ex,
                        cancellationToken);
                    errors.Add(ex.Message);
                    stopExecution = executionMode != ExecutionMode.BestEffort;
                }
                finally
                {
                    IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes =
                        DetectChanges(beforeFacts, facts.AsReadOnly());
                    activity?.SetTag("rule.success", result.IsSuccess);
                    activity?.SetTag("rule.duration_ms", sw.Elapsed.TotalMilliseconds);
                    foreach (KeyValuePair<string, (object? OldValue, object? NewValue)> change in changes)
                    {
                        activity?.SetTag($"fact.{change.Key}.old", change.Value.OldValue);
                        activity?.SetTag($"fact.{change.Key}.new", change.Value.NewValue);
                    }

                    ruleResults[rule.Code] = result;
                    RuleEngineTelemetry.RuleEvalDuration.Record(sw.Elapsed.TotalMilliseconds, tags);
                    RuleEngineTelemetry.RulesFired.Add(1, tags);
                    await NotifyFired(rule, result, facts, changes, context, sw.Elapsed, cancellationToken);
                }

                if (stopExecution)
                {
                    if (executionMode == ExecutionMode.CompensateOnFailure)
                    {
                        await CompensateAsync(executedCompensatableRules, context, facts, compensationErrors,
                            cancellationToken);
                    }

                    break;
                }
            }

            if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId) && executedRuleCount > 0)
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.RuleExecutionsPerDay,
                    executedRuleCount,
                    cancellationToken);
            }

            if (errors.Count == 0)
            {
                return OrchestratorResult.Success(executionMode, facts, ruleResults);
            }

            return OrchestratorResult.Failure(executionMode, facts, ruleResults, errors, compensationErrors);
        }
        finally
        {
            if (_quotaTracker is not null && !string.IsNullOrWhiteSpace(tenantId) && concurrentReserved)
            {
                await _quotaTracker.IncrementUsageAsync(
                    tenantId,
                    QuotaType.ConcurrentExecutions,
                    -1,
                    cancellationToken);
            }
        }
    }

    private async Task CompensateAsync(
        IReadOnlyList<ICompensatableRule<TContext>> executedCompensatableRules,
        TContext context,
        FactBag facts,
        List<string> compensationErrors,
        CancellationToken cancellationToken)
    {
        for (int idx = executedCompensatableRules.Count - 1; idx >= 0; idx--)
        {
            ICompensatableRule<TContext> rule = executedCompensatableRules[idx];
            try
            {
                await rule.CompensateAsync(context, facts, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string message = $"Compensation failed for rule {rule.Code}: {ex.Message}";
                await TraceRuleAsync(
                    rule,
                    context,
                    facts,
                    new Dictionary<string, object?>(facts.AsReadOnly()),
                    RuleTracePhase.Compensate,
                    elapsed: TimeSpan.Zero,
                    isSuccess: false,
                    failureReason: message,
                    exception: ex,
                    cancellationToken);
                compensationErrors.Add(message);
                logger?.Error(ex, "Compensation failed for rule {Rule}", rule.Name);
            }
        }
    }

    private static string BuildRuleFailureMessage(IRule<TContext> rule, RuleResult result)
    {
        return result.Errors.Count > 0
            ? string.Join("; ", result.Errors)
            : $"Rule {rule.Name} failed.";
    }

    private async Task RunHooks(HookPoint point, IRule<TContext> rule, RuleResult result, FactBag facts,
        TContext context, TimeSpan? duration, CancellationToken token)
    {
        foreach (IHookHandler<TContext> hook in hooks)
        {
            try
            {
                await hook.HandleAsync(point, rule, result, facts, context, duration, token);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Hook execution failed at {Point} for {Rule}", point, rule.Name);
            }
        }
    }

    private async Task NotifyMatched(IRule<TContext> rule, TContext context, FactBag facts, CancellationToken token)
    {
        foreach (IRuleEventListener<TContext> listener in _listeners)
        {
            try
            {
                await listener.OnRuleMatchedAsync(rule, context, facts, token);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Event listener failed for rule {Rule} (matched)", rule.Name);
            }
        }
    }

    private async Task NotifyFired(IRule<TContext> rule, RuleResult result, FactBag facts,
        IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes, TContext context, TimeSpan duration,
        CancellationToken token)
    {
        foreach (IRuleEventListener<TContext> listener in _listeners)
        {
            try
            {
                await listener.OnRuleFiredAsync(rule, result, facts, changes, context, duration, token);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Event listener failed for rule {Rule} (fired)", rule.Name);
            }
        }
    }

    private async ValueTask TraceRuleAsync(
        IRule<TContext> rule,
        TContext context,
        FactBag facts,
        IReadOnlyDictionary<string, object?> factsBefore,
        RuleTracePhase phase,
        TimeSpan elapsed,
        bool isSuccess,
        string? failureReason,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        if (_tracer is null)
        {
            return;
        }

        string? tenantId = ResolveTenantId(context);
        if (!_tracer.IsEnabled(tenantId))
        {
            return;
        }

        try
        {
            ISystemExecutionContext executionContext = _contextAccessor?.Get() ?? SystemExecutionContext.Empty;
            IReadOnlyDictionary<string, object?> afterFacts = facts.AsReadOnly();
            IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes =
                DetectChanges(new Dictionary<string, object?>(factsBefore), afterFacts);

            RuleTraceEntry entry = new()
            {
                TenantId = tenantId ?? string.Empty,
                CorrelationId = ResolveCorrelationId(executionContext),
                TraceSessionId = _traceContext?.Current?.SessionId,
                UserId = executionContext.UserId ?? string.Empty,
                SourceType = executionContext.SourceType,
                RuleName = rule.Name,
                RuleSetVersion = rule.GetType().Assembly.GetName().Version?.ToString() ?? "unknown",
                Phase = phase,
                ExecutedAt = DateTimeOffset.UtcNow,
                ElapsedMs = (long)elapsed.TotalMilliseconds,
                IsSuccess = isSuccess,
                FailureReason = failureReason,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception?.Message,
                InputFactsJson = SerializeFacts(factsBefore),
                OutputFactsJson = SerializeFacts(afterFacts),
                ChangedFactKeys = [.. changes.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)]
            };

            await _tracer.TraceAsync(entry, cancellationToken);
        }
        catch
        {
            // Never let tracing impact rule execution pipeline.
        }
    }

    private static string ResolveCorrelationId(ISystemExecutionContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            return context.CorrelationId;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private string? SerializeFacts(IReadOnlyDictionary<string, object?> facts)
    {
        try
        {
            return _jsonSerializeService.Serialize(facts);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, (object? OldValue, object? NewValue)> DetectChanges(
        Dictionary<string, object?> before, IReadOnlyDictionary<string, object?> after)
    {
        Dictionary<string, (object?, object?)> diff = [];
        foreach ((string key, object? value) in after)
        {
            before.TryGetValue(key, out object? oldValue);
            if (!Equals(oldValue, value))
            {
                diff[key] = (oldValue, value);
            }
        }

        foreach ((string key, object? oldValue) in before)
        {
            if (!after.ContainsKey(key))
            {
                diff[key] = (oldValue, null);
            }
        }

        return diff;
    }

    private static List<IRule<TContext>> Order(IEnumerable<IRule<TContext>> rules)
    {
        Dictionary<string, IRule<TContext>> map = rules.ToDictionary(r => r.Code);
        Dictionary<Type, string> typeMap = [];
        foreach (IRule<TContext>? rule in map.Values)
        {
            _ = typeMap.TryAdd(rule.GetType(), rule.Code);
        }

        List<IRule<TContext>> result = [];
        HashSet<string> visiting = [];
        HashSet<string> visited = [];

        foreach (string code in map.Values
                     .OrderBy(rule => rule.Order)
                     .ThenBy(rule => rule.Code, StringComparer.OrdinalIgnoreCase)
                     .Select(rule => rule.Code))
        {
            Visit(code);
        }

        return result;

        void Visit(string code)
        {
            if (visited.Contains(code))
            {
                return;
            }

            if (!visiting.Add(code))
            {
                throw new MInternalException("Cyclic rule dependency detected.", "CIRCULAR_DEPENDENCY");
            }

            foreach (string dep in map[code].DependsOn
                         .OrderBy(dep => map[dep].Order)
                         .ThenBy(dep => dep, StringComparer.OrdinalIgnoreCase))
            {
                if (!map.ContainsKey(dep))
                {
                    throw new MNotFoundException("RuleDependency", dep);
                }

                Visit(dep);
            }

            foreach (Type depType in map[code].Dependencies)
            {
                if (!typeMap.TryGetValue(depType, out string? depCode))
                {
                    throw new MNotFoundException("RuleDependency", depType.Name);
                }

                Visit(depCode);
            }

            visiting.Remove(code);
            visited.Add(code);
            result.Add(map[code]);
        }
    }

    private static string? ResolveTenantId(TContext context)
    {
        if (context is null)
        {
            return Activity.Current?.GetTagItem("tenant.id") as string;
        }

        PropertyInfo? tenantIdProperty = context.GetType().GetProperty(
            "TenantId",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (tenantIdProperty?.GetValue(context) is string tenantId && !string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        return Activity.Current?.GetTagItem("tenant.id") as string;
    }
}

