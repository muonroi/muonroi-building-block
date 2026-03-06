

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Default implementation of <see cref="IRuleFactory{TContext}"/> using <see cref="IServiceProvider"/>.
/// </summary>
public sealed class DefaultRuleFactory<TContext>(IServiceProvider provider) : IRuleFactory<TContext>
{
    public IRule<TContext> Create(Type ruleType)
    {
        return (IRule<TContext>)provider.GetRequiredService(ruleType);
    }
}
