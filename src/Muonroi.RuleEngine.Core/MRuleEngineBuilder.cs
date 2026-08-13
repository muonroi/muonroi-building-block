namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Fluent registration helper for a context-specific rule orchestration pipeline.
/// </summary>
public sealed class MRuleEngineBuilder<TContext>(IServiceCollection services)
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a rule for the context.
    /// </summary>
    /// <typeparam name="TRule">The type of the rule.</typeparam>
    /// <returns>The builder instance.</returns>
    public MRuleEngineBuilder<TContext> AddRule<TRule>()
        where TRule : class, IRule<TContext>
    {
        Services.AddScoped<IRule<TContext>, TRule>();
        return this;
    }

    /// <summary>
    /// Registers a hook for the context.
    /// </summary>
    /// <typeparam name="THook">The type of the hook.</typeparam>
    /// <returns>The builder instance.</returns>
    public MRuleEngineBuilder<TContext> AddHook<THook>()
        where THook : class, IHookHandler<TContext>
    {
        Services.AddScoped<IHookHandler<TContext>, THook>();
        return this;
    }

    /// <summary>
    /// Registers a listener for the context.
    /// </summary>
    /// <typeparam name="TListener">The type of the listener.</typeparam>
    /// <returns>The builder instance.</returns>
    public MRuleEngineBuilder<TContext> AddListener<TListener>()
        where TListener : class, IRuleEventListener<TContext>
    {
        Services.AddScoped<IRuleEventListener<TContext>, TListener>();
        return this;
    }
}
