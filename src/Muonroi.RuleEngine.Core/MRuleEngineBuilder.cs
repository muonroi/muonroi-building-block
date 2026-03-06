namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Fluent registration helper for a context-specific rule orchestration pipeline.
/// </summary>
public sealed class MRuleEngineBuilder<TContext>(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public MRuleEngineBuilder<TContext> AddRule<TRule>()
        where TRule : class, IRule<TContext>
    {
        Services.AddScoped<IRule<TContext>, TRule>();
        return this;
    }

    public MRuleEngineBuilder<TContext> AddHook<THook>()
        where THook : class, IHookHandler<TContext>
    {
        Services.AddScoped<IHookHandler<TContext>, THook>();
        return this;
    }

    public MRuleEngineBuilder<TContext> AddListener<TListener>()
        where TListener : class, IRuleEventListener<TContext>
    {
        Services.AddScoped<IRuleEventListener<TContext>, TListener>();
        return this;
    }
}
