namespace Muonroi.Resilience;

/// <summary>
/// Service collection extensions for registering standard Muonroi resilience policies.
/// </summary>
public static class MuonroiResilienceExtensions
{
    /// <summary>
    /// Registers the standard Muonroi resilience pipeline.
    /// </summary>
    public static IServiceCollection AddMuonroiResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline("muonroi-standard", (builder, context) =>
        {
            IMLog? logger = context.ServiceProvider.GetService<IMLog>();

            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<MTransientException>().Handle<HttpRequestException>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                OnRetry = args =>
                {
                    logger?.Warn("Retrying operation due to {Exception}. Attempt: {Attempt}",
                        args.Outcome.Exception?.Message, args.AttemptNumber);

                    // D-03: Track retry attempt
                    MuonroiMetrics.RetryAttemptCount.Add(1, new KeyValuePair<string, object?>("exception.type", args.Outcome.Exception?.GetType().Name));

                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(TimeSpan.FromSeconds(10));
        });

        return services;
    }
}
