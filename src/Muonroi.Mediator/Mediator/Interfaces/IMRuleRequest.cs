using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Marker interface for requests that participate in the rule engine pipeline.
/// <see cref="Muonroi.Mediator.Behaviours.MRuleEngineBehavior{TRequest,TResponse,TRuleContext}"/>
/// runs rules with <see cref="HookPoint.BeforeRule"/> before the handler
/// and <see cref="HookPoint.AfterRule"/> after the handler.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <typeparam name="TRuleContext">The rule context type that <see cref="IRule{TContext}"/> implementations expect.</typeparam>
public interface IMRuleRequest<out TResponse, out TRuleContext> : IRequest<TResponse>
    where TRuleContext : class, IRuleContext
{
    /// <summary>Builds the rule context for this request instance.</summary>
    TRuleContext BuildRuleContext();

    /// <summary>Override to change the execution mode for rule evaluation.</summary>
    ExecutionMode RuleExecutionMode => ExecutionMode.AllOrNothing;
}
