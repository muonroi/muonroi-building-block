using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering rule engine infrastructure services in ASP.NET Core.
/// </summary>
public static class RuleEngineInfrastructureExtensions
{
    /// <summary>
    /// Registers all rule engine infrastructure services: RuleEngine store, IRuleChangeStore and IRuleChangeProposalStore.
    /// Call this in addition to <see cref="InfrastructureExtensions.AddInfrastructure"/> when you want rule engine support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRuleEngineInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRuleEngineStore(configuration);
        services.AddSingleton<IRuleChangeStore, InMemoryRuleChangeStore>();
        services.AddSingleton<IRuleChangeProposalStore, InMemoryRuleChangeProposalStore>();
        return services;
    }
}
