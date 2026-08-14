namespace Muonroi.RuleEngine.Runtime.Web;

/// <summary>
/// Service registration helpers for rule engine runtime web endpoints.
/// </summary>
public static class RuleEngineRuntimeWebExtensions
{
    /// <summary>Registers runtime web services, endpoints, and SignalR hub support.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration root.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRuleEngineRuntimeWeb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.RequireMinimumTierFromProof(LicenseTier.Licensed, "rule-engine.runtime.web");
        services.AddRuleEngineStore(configuration);
        services.AddRuleEngineTracing(options =>
            configuration.GetSection(RuleTracingOptions.SectionName).Bind(options));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, RuntimeRuleSetManifestContributor>());
        services.TryAddScoped<IRuleDryRunService, RuleDryRunService>();
        services.TryAddSingleton(sp => new MRuleAuthoringManifestRegistry(sp));
        services.TryAddScoped<IMRuleFlowContractProvider, MDefaultRuleFlowContractProvider>();
        services.AddSignalR();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RuleSetHubNotifier>());
        services.AddControllers().AddApplicationPart(typeof(RuleEngineRuntimeWebExtensions).Assembly);
        return services;
    }
}
