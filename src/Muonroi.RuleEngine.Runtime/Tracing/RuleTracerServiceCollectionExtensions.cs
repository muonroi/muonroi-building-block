using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.Core.Tracing;

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
        MGuard.NotNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Register default redactor — consumers can override by registering their own ITraceRedactor before calling this
        services.TryAddSingleton<ITraceRedactor, DefaultTraceRedactor>();
        services.TryAddSingleton<IRuleTraceStore, RedisRuleTraceStore>();
        services.TryAddSingleton<IRuleDebuggerModeService, RuleDebuggerModeService>();
        services.TryAddSingleton<IRuleExecutionTracer, RuleExecutionTracer>();
        return services;
    }
}
