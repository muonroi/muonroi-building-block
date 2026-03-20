namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// Service registration helpers for rule tracing.
/// </summary>
public static class RuleTracerServiceCollectionExtensions
{
    /// <summary>Registers rule tracing services and options.</summary>
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
