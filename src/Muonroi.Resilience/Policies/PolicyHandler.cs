using Muonroi.Logging.Abstractions;

namespace Muonroi.Resilience.Policies;

/// <summary>
/// Builds resilience pipelines with retry, circuit breaker, and timeout policies.
/// </summary>
/// <param name="logger">Logger used for policy events.</param>
public class PolicyHandler(IMLog<PolicyHandler> logger)
{
    /// <summary>
    /// Creates a default resilience pipeline for the specified service.
    /// </summary>
    /// <typeparam name="T">Result type handled by the pipeline.</typeparam>
    /// <param name="serviceName">Service name for log messages.</param>
    /// <returns>The configured resilience pipeline.</returns>
    public ResiliencePipeline<T> CreateDefaultPipeline<T>(string serviceName)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                OnRetry = args =>
                {
                    logger.LogWarning("Retrying {ServiceName} due to {Exception}. Attempt: {Attempt}",
                        serviceName, args.Outcome.Exception?.Message, args.AttemptNumber);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>().Handle<Exception>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    logger.LogError("Circuit breaker opened for {ServiceName} for {BreakDuration}s",
                        serviceName, args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    logger?.Info("Circuit breaker closed for {ServiceName}", serviceName);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }
}
