namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Resilience configuration for connector execution. Used by Polly pipeline.
/// </summary>
public sealed class ConnectorResilienceConfig
{
    public int RetryCount { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public double CircuitBreakerThreshold { get; init; } = 0.5;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public int CircuitBreakerMinimumThroughput { get; init; } = 5;
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
