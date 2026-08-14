namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Possible health levels for the Ollama inference service.
/// </summary>
public enum InfraHealthLevel
{
    /// <summary>Ollama is responding quickly and has capacity.</summary>
    Healthy,

    /// <summary>Ollama is responding slowly and is under load.</summary>
    Degraded,

    /// <summary>Ollama is unreachable.</summary>
    Unavailable
}

/// <summary>
/// Snapshot of Ollama health at a point in time.
/// </summary>
public sealed record InfraHealthStatus(
    InfraHealthLevel Level,
    double BudgetMultiplier,
    string RecommendedModel,
    TimeSpan LastResponseTime,
    DateTimeOffset CheckedAt);

/// <summary>
/// Monitors Ollama infrastructure health and provides adaptive budget multiplier and model recommendations.
/// </summary>
public interface IInfraHealthMonitor
{
    /// <summary>
    /// Check Ollama health. Result is cached for <see cref="ProliferationOptions.InfraHealthCheckIntervalSeconds"/>.
    /// </summary>
    Task<InfraHealthStatus> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementation of <see cref="IInfraHealthMonitor"/> that probes <c>GET {OllamaEndpoint}/api/tags</c>.
/// </summary>
/// <remarks>Creates an infrastructure health monitor.</remarks>
public sealed class InfraHealthMonitor(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IMLog<InfraHealthMonitor>? logger = null) : IInfraHealthMonitor
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ProliferationOptions _options = options;
    private readonly IMLog<InfraHealthMonitor>? _logger = logger;

    private InfraHealthStatus? _cachedStatus;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<InfraHealthStatus> CheckHealthAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Return cached value if still valid (no lock needed for read)
        if (_cachedStatus is not null && now < _cacheExpiry)
            return _cachedStatus;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check under lock
            if (_cachedStatus is not null && now < _cacheExpiry)
                return _cachedStatus;

            InfraHealthStatus fresh = await ProbeOllamaAsync(ct);
            _cachedStatus = fresh;
            _cacheExpiry = now.AddSeconds(_options.InfraHealthCheckIntervalSeconds);

            _logger?.Debug("Infra health: {Level} | ResponseTime: {ResponseTime}ms | BudgetMultiplier: {Multiplier} | Model: {Model}",
                fresh.Level, fresh.LastResponseTime.TotalMilliseconds, fresh.BudgetMultiplier, fresh.RecommendedModel);

            return fresh;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<InfraHealthStatus> ProbeOllamaAsync(CancellationToken ct)
    {
        string endpoint = EndpointValidator.ValidateLocal(_options.OllamaEndpoint, "/api/tags");
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s probe timeout

            HttpClient client = _httpClientFactory.CreateClient("OllamaProliferation");
            HttpResponseMessage response = await client.GetAsync(endpoint, timeoutCts.Token);
            sw.Stop();

            response.EnsureSuccessStatusCode();

            TimeSpan elapsed = sw.Elapsed;
            return ClassifyResponseTime(elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger?.Warn("Ollama health probe timed out after 10s");
            return new InfraHealthStatus(
                InfraHealthLevel.Unavailable,
                BudgetMultiplier: 0.25,
                RecommendedModel: _options.FallbackModel,
                LastResponseTime: sw.Elapsed,
                CheckedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.Warn("Ollama health probe failed: {Error}", ex.Message);
            return new InfraHealthStatus(
                InfraHealthLevel.Unavailable,
                BudgetMultiplier: 0.25,
                RecommendedModel: _options.FallbackModel,
                LastResponseTime: sw.Elapsed,
                CheckedAt: DateTimeOffset.UtcNow);
        }
    }

    private InfraHealthStatus ClassifyResponseTime(TimeSpan elapsed)
    {
        double ms = elapsed.TotalMilliseconds;

        if (ms < _options.InfraFastThresholdMs)
        {
            // Fast response → increase budget, use primary model
            return new InfraHealthStatus(
                InfraHealthLevel.Healthy,
                BudgetMultiplier: 1.5,
                RecommendedModel: _options.PrimaryModel,
                LastResponseTime: elapsed,
                CheckedAt: DateTimeOffset.UtcNow);
        }

        if (ms > _options.InfraSlowThresholdMs)
        {
            // Slow response → halve budget, downgrade to fallback model
            return new InfraHealthStatus(
                InfraHealthLevel.Degraded,
                BudgetMultiplier: 0.5,
                RecommendedModel: _options.FallbackModel,
                LastResponseTime: elapsed,
                CheckedAt: DateTimeOffset.UtcNow);
        }

        // Mid-range response → normal budget, primary model
        return new InfraHealthStatus(
            InfraHealthLevel.Healthy,
            BudgetMultiplier: 1.0,
            RecommendedModel: _options.PrimaryModel,
            LastResponseTime: elapsed,
            CheckedAt: DateTimeOffset.UtcNow);
    }
}
