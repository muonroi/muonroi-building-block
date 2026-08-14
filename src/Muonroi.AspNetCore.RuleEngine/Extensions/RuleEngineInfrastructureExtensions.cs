namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering rule engine infrastructure services in ASP.NET Core.
/// </summary>
public static class RuleEngineInfrastructureExtensions
{
    /// <summary>
    /// Registers all rule engine infrastructure services: RuleEngine store, IRuleChangeStore,
    /// IRuleChangeProposalStore, and generic controller wiring (GenericControllerRouteConvention,
    /// GenericControllerFeatureProvider).
    /// Call this in addition to <see cref="InfrastructureExtensions.AddInfrastructure"/> when you want rule engine support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="assemblies">Assemblies to scan for MEntity subclasses for generic controller registration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRuleEngineInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        services.AddRuleEngineStore(configuration);
        services.AddSingleton<IRuleChangeStore, InMemoryRuleChangeStore>();
        services.AddSingleton<IRuleChangeProposalStore, InMemoryRuleChangeProposalStore>();

        // Wire up generic controller support for MGenericController<TEntity, TDbContext>
        services.AddControllers(options =>
        {
            options.Conventions.Add(new GenericControllerRouteConvention());
        })
        .ConfigureApplicationPartManager(manager =>
        {
            manager.FeatureProviders.Add(assemblies.Length > 0
                ? new GenericControllerFeatureProvider(assemblies)
                : new GenericControllerFeatureProvider());
        });

        return services;
    }
}
