using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Contributors;
using Muonroi.RuleEngine.Runtime.Web.Models;
using Muonroi.RuleEngine.Runtime.Web.Services;
using Muonroi.RuleEngine.Runtime.Tracing;
using Muonroi.Governance.License;

namespace Muonroi.RuleEngine.Runtime.Web;

public static class RuleEngineRuntimeWebExtensions
{
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
