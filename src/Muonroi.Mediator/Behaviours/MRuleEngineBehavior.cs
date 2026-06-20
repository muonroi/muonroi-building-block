using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Exceptions;
using Muonroi.Mediator.Mediator.Attributes;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Telemetry;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Integrates the Muonroi Rule Engine into the mediator pipeline for requests implementing <see cref="IMRuleRequest{TResponse,TRuleContext}"/>.
/// </summary>
/// <typeparam name="TRequest">The mediator request type.</typeparam>
/// <typeparam name="TResponse">The mediator response type.</typeparam>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Rule engine uses reflection and object? returns; unconstrained generics prevent MGuard.NotNull usage; null is validated via is-not-null patterns.")]
public sealed class MRuleEngineBehavior<TRequest, TResponse>(
    ServiceFactory serviceFactory,
    IMLog<MRuleEngineBehavior<TRequest, TResponse>>? logger = null,
    IMTraceContext? traceContext = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, RuleMetadata?> MetadataCache = new();

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        RuleMetadata? metadata = GetRuleMetadata();
        if (metadata == null)
        {
            return await next();
        }

        using IDisposable? behaviorNode = traceContext?.Current?.BeginNode("RuleEngine", MTraceNodeType.PipelineBehavior);

        object ruleRequest = request;
        object ctx = metadata.BuildRuleContextMethod.Invoke(ruleRequest, null)!;
        Type rulesType = typeof(IEnumerable<>).MakeGenericType(typeof(IRule<>).MakeGenericType(metadata.RuleContextType));

        if (serviceFactory(rulesType) is not IEnumerable<object> rules)
        {
            return await next();
        }

        List<object> ruleList = [.. rules];
        if (ruleList.Count == 0)
        {
            return await next();
        }

        IMediator? mediator = serviceFactory(typeof(IMediator)) as IMediator;

        await ExecutePhaseAsync(ruleList, HookPoint.BeforeRule, ctx, metadata, mediator, throwOnFailure: true, cancellationToken);

        TResponse response = await next();

        await ExecutePhaseAsync(ruleList, HookPoint.AfterRule, ctx, metadata, mediator, throwOnFailure: false, cancellationToken);

        return response;
    }

    /// <summary>
    /// Executes the rules registered for a specific mediator hook point.
    /// </summary>
    /// <param name="allRules">All rules resolved for the request context.</param>
    /// <param name="phase">The hook point currently being executed.</param>
    /// <param name="ctx">The runtime rule context.</param>
    /// <param name="metadata">Cached reflection metadata for the request.</param>
    /// <param name="mediator">The mediator used to publish notifications.</param>
    /// <param name="throwOnFailure">When true, rule failures stop the pipeline.</param>
    /// <param name="ct">The cancellation token.</param>
    private async Task ExecutePhaseAsync(
        List<object> allRules,
        HookPoint phase,
        object ctx,
        RuleMetadata metadata,
        IMediator? mediator,
        bool throwOnFailure,
        CancellationToken ct)
    {
        IEnumerable<ResolvedRule> phaseRules = allRules
            .Select(ResolvedRule.FromRule)
            .Where(x => x.HookPoint == phase)
            .OrderBy(static x => x.Order);

        foreach (ResolvedRule resolvedRule in phaseRules)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            object rule = resolvedRule.Rule;
            ITraceSession? session = traceContext?.Current;
            using IDisposable? ruleNode = session?.BeginNode(resolvedRule.Code, MTraceNodeType.Rule);

            try
            {
                FactBag facts = new();
                session?.RecordFactSnapshot("before", facts.AsReadOnly());

                Task<RuleResult> evalTask = (Task<RuleResult>)metadata.EvaluateAsyncMethod.Invoke(rule, [ctx, facts, ct])!;
                RuleResult result = await evalTask;
                session?.RecordFactSnapshot("after", facts.AsReadOnly());

                if (result.IsSuccess)
                {
                    Task execTask = (Task)metadata.ExecuteAsyncMethod.Invoke(rule, [ctx, ct])!;
                    await execTask;

                    long elapsedMs = GetElapsedMilliseconds(startTimestamp);
                    using IMLogContextScope? completedScope = BeginLogScope(resolvedRule.Code, phase, passed: true, elapsedMs);
                    await EmitNotificationsAsync(rule, ctx, metadata, mediator, phase, ct);
                    RecordTelemetry(resolvedRule.Code, phase, true, elapsedMs);
                    logger?.Info("[Rule] {Code} PASS ({ElapsedMs}ms)", resolvedRule.Code, elapsedMs);
                    continue;
                }

                long failedElapsedMs = GetElapsedMilliseconds(startTimestamp);
                using IMLogContextScope? failedScope = BeginLogScope(resolvedRule.Code, phase, passed: false, failedElapsedMs);
                RecordTelemetry(resolvedRule.Code, phase, false, failedElapsedMs);
                string joinedErrors = string.Join("; ", result.Errors);
                logger?.Warn("[Rule] {Code} FAIL ({ElapsedMs}ms): {Errors}", resolvedRule.Code, failedElapsedMs, joinedErrors);

                if (throwOnFailure)
                {
                    throw new MRuleViolationException(resolvedRule.Code, result.Errors);
                }
            }
            catch (Exception ex) when (ex is not MRuleViolationException)
            {
                long elapsedMs = GetElapsedMilliseconds(startTimestamp);
                using IMLogContextScope? failedScope = BeginLogScope(resolvedRule.Code, phase, passed: false, elapsedMs);
                RecordTelemetry(resolvedRule.Code, phase, false, elapsedMs);
                Exception loggedException = UnwrapInvocationException(ex);
                logger?.Error(loggedException, "[Rule] {Code} ERROR ({ElapsedMs}ms)", resolvedRule.Code, elapsedMs);
                throw;
            }
        }
    }

    /// <summary>
    /// Publishes notifications declared on a rule after it passes and executes successfully.
    /// </summary>
    /// <param name="rule">The rule instance that passed.</param>
    /// <param name="ctx">The runtime rule context.</param>
    /// <param name="metadata">Cached metadata describing the rule context type.</param>
    /// <param name="mediator">The mediator used to publish notifications.</param>
    /// <param name="phase">The execution phase that produced the notification.</param>
    /// <param name="ct">The cancellation token.</param>
    private async Task EmitNotificationsAsync(
        object rule,
        object ctx,
        RuleMetadata metadata,
        IMediator? mediator,
        HookPoint phase,
        CancellationToken ct)
    {
        if (mediator == null)
        {
            return;
        }

        MEmitOnPassAttribute[] attributes = [.. rule.GetType().GetCustomAttributes<MEmitOnPassAttribute>(inherit: false)];
        if (attributes.Length == 0)
        {
            return;
        }

        foreach (MEmitOnPassAttribute attribute in attributes)
        {
            try
            {
                INotification notification = BuildNotification(rule, ctx, metadata, attribute);
                await mediator.Publish(notification, ct);
            }
            catch (Exception ex)
            {
                Exception loggedException = UnwrapInvocationException(ex);
                logger?.Error(
                    loggedException,
                    "[Rule] {Code} notification emit failed during {Phase} for {NotificationType}",
                    ResolvedRuleCode(rule),
                    phase,
                    attribute.NotificationType.FullName ?? attribute.NotificationType.Name);
            }
        }
    }

    /// <summary>
    /// Resolves a rule code for logging and notification error handling.
    /// </summary>
    /// <param name="rule">The rule instance.</param>
    /// <returns>The configured rule code, or the type name when the code cannot be resolved.</returns>
    private static string ResolvedRuleCode(object rule)
    {
        return rule.GetType().GetProperty(nameof(IRule<object>.Code))?.GetValue(rule)?.ToString() ?? rule.GetType().Name;
    }

    /// <summary>
    /// Creates the mediator notification to publish for a successful rule execution.
    /// </summary>
    /// <param name="rule">The rule instance that passed.</param>
    /// <param name="ctx">The runtime rule context.</param>
    /// <param name="metadata">Cached metadata describing the rule context type.</param>
    /// <param name="attribute">The notification declaration attached to the rule.</param>
    /// <returns>The notification instance to publish.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configured notification type is invalid.</exception>
    private static INotification BuildNotification(
        object rule,
        object ctx,
        RuleMetadata metadata,
        MEmitOnPassAttribute attribute)
    {
        Type factoryType = typeof(IRuleNotificationFactory<>).MakeGenericType(metadata.RuleContextType);
        if (factoryType.IsAssignableFrom(rule.GetType()))
        {
            MethodInfo buildMethod = factoryType.GetMethod(nameof(IRuleNotificationFactory<object>.BuildNotification))!;
            object? built = buildMethod.Invoke(rule, [ctx]);
            if (built is INotification builtNotification)
            {
                return builtNotification;
            }

            throw new MInternalException($"Rule '{ResolvedRuleCode(rule)}' returned an invalid notification instance.");
        }

        object? notification = Activator.CreateInstance(attribute.NotificationType);
        if (notification is INotification typedNotification)
        {
            return typedNotification;
        }

        throw new MInternalException($"Notification type '{attribute.NotificationType.FullName}' must implement INotification.");
    }

    /// <summary>
    /// Begins a structured logging scope for a single rule execution.
    /// </summary>
    /// <param name="ruleCode">The rule code.</param>
    /// <param name="phase">The execution phase.</param>
    /// <param name="passed">Whether the rule passed.</param>
    /// <param name="elapsedMs">The elapsed duration in milliseconds.</param>
    /// <returns>A disposable logging scope when logging is enabled; otherwise null.</returns>
    private IMLogContextScope? BeginLogScope(string ruleCode, HookPoint phase, bool passed, long elapsedMs)
    {
        if (logger == null)
        {
            return null;
        }

        RuleExecutionLogScope scope = new(
            RuleCode: ruleCode,
            HookPoint: phase.ToString(),
            Passed: passed,
            ElapsedMs: elapsedMs,
            TenantId: executionContextAccessor?.Get().TenantId);
        return logger.BeginProperty("RuleExecution", scope);
    }

    /// <summary>
    /// Records mediator rule execution metrics using the shared rule engine meter.
    /// </summary>
    /// <param name="ruleCode">The unique rule code.</param>
    /// <param name="phase">The execution phase.</param>
    /// <param name="passed">Whether the rule passed.</param>
    /// <param name="elapsedMs">The elapsed duration in milliseconds.</param>
    private void RecordTelemetry(string ruleCode, HookPoint phase, bool passed, long elapsedMs)
    {
        string tenantId = executionContextAccessor?.Get().TenantId ?? string.Empty;
        TagList tags = new()
        {
            { "rule_code", ruleCode },
            { "tenant_id", tenantId },
            { "phase", phase.ToString() },
            { "passed", passed }
        };

        RuleEngineTelemetry.RuleExecutionResult.Add(1, tags);
        RuleEngineTelemetry.RuleExecutionDurationMs.Record(elapsedMs, tags);
    }

    /// <summary>
    /// Gets the cached reflection metadata required to execute the rule pipeline for the current request type.
    /// </summary>
    /// <returns>The cached metadata, or null when the request is not rule-enabled.</returns>
    private RuleMetadata? GetRuleMetadata()
    {
        return MetadataCache.GetOrAdd(typeof(TRequest), static requestType =>
        {
            Type? ruleInterface = requestType.GetInterfaces()
                .FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMRuleRequest<,>));

            if (ruleInterface == null)
            {
                return null;
            }

            Type ruleContextType = ruleInterface.GetGenericArguments()[1];
            Type ruleType = typeof(IRule<>).MakeGenericType(ruleContextType);

            return new RuleMetadata(
                RuleContextType: ruleContextType,
                BuildRuleContextMethod: requestType.GetMethod("BuildRuleContext")!,
                EvaluateAsyncMethod: ruleType.GetMethod(nameof(IRule<object>.EvaluateAsync))!,
                ExecuteAsyncMethod: ruleType.GetMethod(nameof(IRule<object>.ExecuteAsync))!);
        });
    }

    /// <summary>
    /// Converts a stopwatch timestamp into a bounded millisecond duration.
    /// </summary>
    /// <param name="startTimestamp">The timestamp captured when rule execution started.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    private static long GetElapsedMilliseconds(long startTimestamp)
    {
        return Math.Max(0L, (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }

    /// <summary>
    /// Unwraps reflection wrapper exceptions so logs capture the real failure.
    /// </summary>
    /// <param name="exception">The exception produced by reflection or rule execution.</param>
    /// <returns>The inner exception when available; otherwise the original exception.</returns>
    private static Exception UnwrapInvocationException(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: not null } invocationException
            ? invocationException.InnerException!
            : exception;
    }

    /// <summary>
    /// Cached reflection metadata for a rule-enabled mediator request.
    /// </summary>
    /// <param name="RuleContextType">The context type consumed by the resolved rules.</param>
    /// <param name="BuildRuleContextMethod">The request method that creates the rule context.</param>
    /// <param name="EvaluateAsyncMethod">The reflected rule evaluation method.</param>
    /// <param name="ExecuteAsyncMethod">The reflected rule execution method.</param>
    private sealed record RuleMetadata(
        Type RuleContextType,
        MethodInfo BuildRuleContextMethod,
        MethodInfo EvaluateAsyncMethod,
        MethodInfo ExecuteAsyncMethod);

    /// <summary>
    /// Cached description of a resolved rule instance.
    /// </summary>
    /// <param name="Rule">The raw rule instance.</param>
    /// <param name="HookPoint">The configured rule hook point.</param>
    /// <param name="Order">The configured execution order.</param>
    /// <param name="Code">The configured rule code.</param>
    private sealed record ResolvedRule(object Rule, HookPoint HookPoint, int Order, string Code)
    {
        /// <summary>
        /// Creates a <see cref="ResolvedRule"/> from a raw rule object.
        /// </summary>
        /// <param name="rule">The raw rule instance.</param>
        /// <returns>The resolved rule metadata.</returns>
        public static ResolvedRule FromRule(object rule)
        {
            MGuard.NotNull(rule);
            Type type = rule.GetType();
            return new ResolvedRule(
                Rule: rule,
                HookPoint: (HookPoint)type.GetProperty(nameof(IRule<object>.HookPoint))!.GetValue(rule)!,
                Order: (int)type.GetProperty(nameof(IRule<object>.Order))!.GetValue(rule)!,
                Code: (string)type.GetProperty(nameof(IRule<object>.Code))!.GetValue(rule)!);
        }
    }
}
