using Muonroi.Logging.Abstractions;

namespace Muonroi.Resilience.Policies;

public class PolicyHandler(IMLog<PolicyHandler> logger)
{
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
