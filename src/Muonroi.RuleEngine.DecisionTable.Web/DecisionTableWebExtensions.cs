using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.DecisionTable.Web.Contributors;
using Muonroi.RuleEngine.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Web;

public static class DecisionTableWebExtensions
{
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
