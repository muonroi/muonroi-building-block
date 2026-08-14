namespace Muonroi.RuleEngine.Runtime.Web;

/// <summary>
/// Endpoint mapping helpers for the rule engine runtime web surface.
/// </summary>
public static class RuleEngineRuntimeEndpointExtensions
{
    /// <summary>Maps controllers, tracing endpoints, and SignalR hubs.</summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>The same endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapRuleEngineRuntimeWeb(this IEndpointRouteBuilder endpoints)
    {
        MGuard.NotNull(endpoints);
        endpoints.MapControllers();
        endpoints.MapRuleTracingEndpoints();
        endpoints.MapHub<RuleSetChangeHub>("/hubs/ruleset-changes");
        return endpoints;
    }
}
