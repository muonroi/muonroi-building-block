using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging.Abstractions;
using Muonroi.Observability.OpenTelemetry;
using Polly;

namespace Quickstart.Resilience.Api.Services;

/// <summary>
/// Demonstrates executing HTTP calls wrapped in the "muonroi-standard" resilience pipeline.
/// The pipeline provides automatic retry with exponential back-off, circuit breaker protection,
/// and a hard 10-second execution timeout — all configured via <see cref="MuonroiResilienceExtensions.AddMuonroiResilience"/>.
/// </summary>
public sealed class WeatherApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly IMLog<WeatherApiClient> _logger;

    // Tracks how many deliberate failures have been injected so far in the current demo run.
    private int _failureCount;

    public WeatherApiClient(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMLog<WeatherApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("weather-api");
        // Retrieve the named pipeline registered by AddMuonroiResilience().
        _pipeline = pipelineProvider.GetPipeline("muonroi-standard");
        _logger = logger;
    }

    /// <summary>
    /// Returns the current weather for the supplied coordinates.
    /// The call is automatically retried up to 3 times on <see cref="MTransientException"/>
    /// or <see cref="HttpRequestException"/> before the circuit breaker considers opening.
    /// </summary>
    public async Task<WeatherResult> GetCurrentWeatherAsync(
        double latitude, double longitude,
        CancellationToken cancellationToken = default)
    {
        _logger.Info("Fetching current weather for lat={Lat} lon={Lon}", latitude, longitude);

        WeatherResult result = await _pipeline.ExecuteAsync(async ct =>
        {
            string url = $"forecast?latitude={latitude}&longitude={longitude}" +
                         "&current_weather=true&timezone=auto";

            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Treat HTTP errors as transient so the retry policy picks them up.
                throw new MTransientException(
                    $"Weather API returned {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            _logger.Info("Weather API responded successfully");
            return new WeatherResult(latitude, longitude, json);
        }, cancellationToken);

        return result;
    }

    /// <summary>
    /// Deliberately fails the first <paramref name="failuresBeforeSuccess"/> calls to
    /// demonstrate the retry back-off behaviour. On each failure the pipeline logs a warning
    /// and increments <see cref="MuonroiMetrics.RetryAttemptCount"/>.
    /// </summary>
    public async Task<WeatherResult> GetForecastAsync(
        double latitude, double longitude,
        int failuresBeforeSuccess = 2,
        CancellationToken cancellationToken = default)
    {
        _failureCount = 0;

        _logger.Info(
            "Starting GetForecastAsync — will inject {FailCount} transient failure(s) before succeeding",
            failuresBeforeSuccess);

        WeatherResult result = await _pipeline.ExecuteAsync(async ct =>
        {
            if (_failureCount < failuresBeforeSuccess)
            {
                _failureCount++;
                _logger.Warn("Injecting deliberate transient failure #{Attempt}", _failureCount);

                // MTransientException is handled by the retry predicate in AddMuonroiResilience().
                // The OnRetry callback increments MuonroiMetrics.RetryAttemptCount automatically.
                throw new MTransientException(
                    $"Simulated transient failure #{_failureCount} (retries remaining: {failuresBeforeSuccess - _failureCount})");
            }

            string url = $"forecast?latitude={latitude}&longitude={longitude}" +
                         "&daily=temperature_2m_max,precipitation_sum&timezone=auto";

            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct);
            _logger.Info("Forecast API succeeded after {Failures} injected failure(s)", _failureCount);
            return new WeatherResult(latitude, longitude, json);
        }, cancellationToken);

        return result;
    }
}

/// <summary>Result returned by <see cref="WeatherApiClient"/>.</summary>
public sealed record WeatherResult(double Latitude, double Longitude, string RawJson);
