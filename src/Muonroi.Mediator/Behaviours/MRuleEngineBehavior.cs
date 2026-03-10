using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Exceptions;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Integrates the Muonroi Rule Engine into the mediator pipeline for all requests implementing IMRuleRequest.
/// This version is generic and automatically discovers the TRuleContext from the request type.
/// </summary>
public sealed class MRuleEngineBehavior<TRequest, TResponse>(
    Mediator.ServiceFactory serviceFactory,
    IMLog<MRuleEngineBehavior<TRequest, TResponse>>? logger = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, RuleMetadata?> _metadataCache = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        RuleMetadata? metadata = GetRuleMetadata();
        if (metadata == null) return await next();

        object ruleRequest = request;
        object ctx = metadata.BuildRuleContextMethod.Invoke(ruleRequest, null)!;
        
        // Resolve rules: IEnumerable<IRule<TRuleContext>>
        Type rulesType = typeof(IEnumerable<>).MakeGenericType(typeof(IRule<>).MakeGenericType(metadata.RuleContextType));
        IEnumerable<object>? rules = serviceFactory(rulesType) as IEnumerable<object>;

        if (rules == null)
        {
            return await next();
        }

        var ruleList = rules.ToList();
        if (ruleList.Count == 0)
        {
            return await next();
        }

        // ── Pre-phase ───────────────────────────────────────────────────────────
        await ExecutePhaseAsync(ruleList, HookPoint.BeforeRule, ctx, metadata, throwOnFailure: true, cancellationToken);

        // ── Handler ─────────────────────────────────────────────────────────────
        TResponse response = await next();

        // ── Post-phase ──────────────────────────────────────────────────────────
        await ExecutePhaseAsync(ruleList, HookPoint.AfterRule, ctx, metadata, throwOnFailure: false, cancellationToken);

        return response;
    }

    private async Task ExecutePhaseAsync(
        List<object> allRules,
        HookPoint phase,
        object ctx,
        RuleMetadata metadata,
        bool throwOnFailure,
        CancellationToken ct)
    {
        // Filter rules by phase and order
        var phaseRules = allRules
            .Select(r => new { Rule = r, HookPoint = (HookPoint)r.GetType().GetProperty("HookPoint")!.GetValue(r)!, Order = (int)r.GetType().GetProperty("Order")!.GetValue(r)!, Code = (string)r.GetType().GetProperty("Code")!.GetValue(r)! })
            .Where(x => x.HookPoint == phase)
            .OrderBy(x => x.Order);

        foreach (var x in phaseRules)
        {
            object rule = x.Rule;
            
            // Invoke EvaluateAsync
            Task<RuleResult> evalTask = (Task<RuleResult>)metadata.EvaluateAsyncMethod.Invoke(rule, [ctx, new FactBag(), ct])!;
            RuleResult result = await evalTask;

            if (result.IsSuccess)
            {
                // Invoke ExecuteAsync
                Task execTask = (Task)metadata.ExecuteAsyncMethod.Invoke(rule, [ctx, ct])!;
                await execTask;
                logger?.Debug("[RuleEngine] Rule '{Code}' passed.", x.Code);
            }
            else
            {
                logger?.Warn("[RuleEngine] Rule '{Code}' failed: {Errors}",
                    x.Code, string.Join("; ", result.Errors));

                if (throwOnFailure)
                    throw new MRuleViolationException(x.Code, result.Errors);
            }
        }
    }

    private RuleMetadata? GetRuleMetadata()
    {
        return _metadataCache.GetOrAdd(typeof(TRequest), static requestType =>
        {
            Type? ruleInterface = requestType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMRuleRequest<,>));

            if (ruleInterface == null) return null;

            Type responseType = ruleInterface.GetGenericArguments()[0];
            Type ruleContextType = ruleInterface.GetGenericArguments()[1];

            Type ruleType = typeof(IRule<>).MakeGenericType(ruleContextType);

            return new RuleMetadata(
                ruleContextType,
                requestType.GetMethod("BuildRuleContext")!,
                ruleType.GetMethod("EvaluateAsync")!,
                ruleType.GetMethod("ExecuteAsync")!
            );
        });
    }

    private record RuleMetadata(
        Type RuleContextType,
        MethodInfo BuildRuleContextMethod,
        MethodInfo EvaluateAsyncMethod,
        MethodInfo ExecuteAsyncMethod);
}
