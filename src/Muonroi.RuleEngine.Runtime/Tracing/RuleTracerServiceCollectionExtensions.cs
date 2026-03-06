namespace Muonroi.RuleEngine.Runtime.Tracing;

public static class RuleTracerServiceCollectionExtensions
{
    public static IServiceCollection AddRuleEngineTracing(
        this IServiceCollection services,
        Action<RuleTracingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IRuleTraceStore, RedisRuleTraceStore>();
        services.TryAddSingleton<IRuleDebuggerModeService, RuleDebuggerModeService>();
        services.TryAddSingleton<IRuleExecutionTracer, RuleExecutionTracer>();
        return services;
    }
}
