namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Resilience configuration for connector execution. Used by Polly pipeline.
/// </summary>
public sealed class ConnectorResilienceConfig
{
    /// <summary>Number of retry attempts for transient failures.</summary>
    public int RetryCount { get; init; } = 3;
    /// <summary>Delay between retry attempts.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>Overall timeout for a connector execution.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Failure ratio required to open the circuit breaker.</summary>
    public double CircuitBreakerThreshold { get; init; } = 0.5;
    /// <summary>Sampling duration used to evaluate breaker thresholds.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Minimum throughput before circuit breaker evaluation occurs.</summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 5;
    /// <summary>Duration the circuit remains open before attempting recovery.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
