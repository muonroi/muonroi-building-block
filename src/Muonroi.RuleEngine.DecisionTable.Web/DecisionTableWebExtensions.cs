namespace Muonroi.RuleEngine.DecisionTable.Web;

/// <summary>
/// DI helpers for decision table web features.
/// </summary>
public static class DecisionTableWebExtensions
{
    /// <summary>
    /// Registers decision table web services and controllers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional engine configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddDecisionTableWeb(
        this IServiceCollection services,
        Action<DecisionTableEngineOptions>? configure = null)
    {
        services.RequireMinimumTierFromProof(LicenseTier.Licensed, "decision-table.web");
        services.AddDecisionTableEngine(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, DecisionTableManifestContributor>());
        services.AddControllers()
            .AddApplicationPart(typeof(DecisionTableWebExtensions).Assembly);
        return services;
    }
}
