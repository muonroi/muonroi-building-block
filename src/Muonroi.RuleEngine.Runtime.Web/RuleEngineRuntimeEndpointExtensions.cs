using Muonroi.RuleEngine.Runtime.Tracing;
using Muonroi.RuleEngine.Runtime.Web.Hubs;

namespace Muonroi.RuleEngine.Runtime.Web;

public static class RuleEngineRuntimeEndpointExtensions
{
    public static IEndpointRouteBuilder MapRuleEngineRuntimeWeb(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapControllers();
        endpoints.MapRuleTracingEndpoints();
        endpoints.MapHub<RuleSetChangeHub>("/hubs/ruleset-changes");
        return endpoints;
    }
}
