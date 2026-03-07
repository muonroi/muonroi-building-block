using Microsoft.Extensions.Options;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// A consume filter that runs registered <see cref="IMessageRoutingRule{T}"/> implementations
/// against each incoming message. Rules are resolved from DI; if none are registered for T
/// the filter is a no-op.
///
/// Depends only on <c>Muonroi.Messaging.Abstractions</c> (OSS) — no direct reference to
/// <c>Muonroi.RuleEngine.Core</c> or any concrete orchestrator.
/// </summary>
/// <typeparam name="T">Message type.</typeparam>
public class RuleEngineRoutingFilter<T>(
    IEnumerable<IMessageRoutingRule<T>> routingRules,
    IOptions<MessageBusConfigs> configs) : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly MessageBusConfigs _configs = configs.Value;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (_configs.EnableRuleEngineRouting)
        {
            foreach (IMessageRoutingRule<T> rule in routingRules.OrderBy(r => r.Order))
            {
                // ExecuteAsync throws on rule violation — MassTransit will retry / move to error queue.
                await rule.ExecuteAsync(context.Message, context.CancellationToken);
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
    }
}
