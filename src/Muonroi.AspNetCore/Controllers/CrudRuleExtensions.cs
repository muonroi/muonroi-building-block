namespace Muonroi.AspNetCore.Controllers;

/// <summary>
/// Extension methods for registering business rules with Auto CRUD operations.
/// </summary>
public static class CrudRuleExtensions
{
    /// <summary>
    /// Registers a RuleOrchestrator for the specified entity type to enable business rule execution in Auto CRUD.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureRules">Action to configure and register rules.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrudRules<TEntity>(
        this IServiceCollection services,
        Action<IServiceCollection>? configureRules = null)
        where TEntity : MEntity
    {
        // Register individual rules via the configure action
        configureRules?.Invoke(services);

        // Register the orchestrator with all IRule<CrudContext<TEntity>> implementations
        services.AddScoped(sp =>
        {
            IEnumerable<IRule<CrudContext<TEntity>>> rules = sp.GetServices<IRule<CrudContext<TEntity>>>();
            IEnumerable<IHookHandler<CrudContext<TEntity>>> hooks = sp.GetServices<IHookHandler<CrudContext<TEntity>>>();
            ILogger<RuleOrchestrator<CrudContext<TEntity>>>? logger = sp.GetService<ILogger<RuleOrchestrator<CrudContext<TEntity>>>>();
            IEnumerable<IRuleEventListener<CrudContext<TEntity>>> listeners = sp.GetServices<IRuleEventListener<CrudContext<TEntity>>>();

            return new RuleOrchestrator<CrudContext<TEntity>>(rules, hooks, logger, listeners);
        });

        return services;
    }

    /// <summary>
    /// Registers a business rule for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TRule">The rule implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrudRule<TEntity, TRule>(this IServiceCollection services)
        where TEntity : MEntity
        where TRule : class, IRule<CrudContext<TEntity>>
    {
        services.AddScoped<IRule<CrudContext<TEntity>>, TRule>();
        return services;
    }

    /// <summary>
    /// Registers a hook handler for CRUD operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="THook">The hook handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrudHook<TEntity, THook>(this IServiceCollection services)
        where TEntity : MEntity
        where THook : class, IHookHandler<CrudContext<TEntity>>
    {
        services.AddScoped<IHookHandler<CrudContext<TEntity>>, THook>();
        return services;
    }

    /// <summary>
    /// Registers an event listener for CRUD rule execution.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TListener">The event listener implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrudRuleListener<TEntity, TListener>(this IServiceCollection services)
        where TEntity : MEntity
        where TListener : class, IRuleEventListener<CrudContext<TEntity>>
    {
        services.AddScoped<IRuleEventListener<CrudContext<TEntity>>, TListener>();
        return services;
    }
}
