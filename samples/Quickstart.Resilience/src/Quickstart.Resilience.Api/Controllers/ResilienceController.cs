

namespace Quickstart.Resilience.Api.Controllers;

/// <summary>
/// Demonstrates every aspect of the Muonroi Resilience package.
///
/// Endpoints:
/// <list type="bullet">
///   <item>GET  /api/resilience/weather              — real HTTP call wrapped in "muonroi-standard"</item>
///   <item>GET  /api/resilience/weather/forecast     — call that injects N transient failures first</item>
///   <item>POST /api/resilience/payment              — payment with optional transient-failure injection</item>
///   <item>POST /api/resilience/payment/hard-failure — hard failure that feeds the circuit-breaker counter</item>
///   <item>GET  /api/resilience/demo/retry           — self-contained retry demo (no external I/O)</item>
///   <item>GET  /api/resilience/demo/circuit-breaker — self-contained circuit-breaker demo</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/resilience")]
public sealed class ResilienceController(
    WeatherApiClient weatherApiClient,
    PaymentService paymentService,
    ResiliencePipelineProvider<string> pipelineProvider,
    IMLog<ResilienceController> logger) : ControllerBase
{
    // -------------------------------------------------------------------------
    // Weather endpoints
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calls the Open-Meteo weather API for Berlin wrapped in the "muonroi-standard" pipeline.
    /// Retry and timeout are applied automatically.
    /// </summary>
    [HttpGet("weather")]
    public async Task<IActionResult> GetCurrentWeatherAsync(CancellationToken ct)
    {
        try
        {
            WeatherResult result = await weatherApiClient.GetCurrentWeatherAsync(52.52, 13.41, ct);
            return Ok(new
            {
                description = "Current weather retrieved via 'muonroi-standard' resilience pipeline.",
                result.Latitude,
                result.Longitude,
                result.RawJson
            });
        }
        catch (Exception ex)
        {
            logger.Warn("GetCurrentWeatherAsync failed: {Message}", ex.Message);
            return StatusCode(503, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Requests a forecast but deliberately injects transient failures first.
    /// Watch the logs for "Retrying operation due to …" messages and observe
    /// <c>muonroi.retry.attempts</c> counter via any OTel-compatible metrics sink.
    /// </summary>
    [HttpGet("weather/forecast")]
    public async Task<IActionResult> GetForecastAsync(
        [FromQuery] int failuresBeforeSuccess = 2,
        CancellationToken ct = default)
    {
        try
        {
            WeatherResult result = await weatherApiClient.GetForecastAsync(
                52.52, 13.41, failuresBeforeSuccess, ct);

            return Ok(new
            {
                description = $"Forecast succeeded after {failuresBeforeSuccess} injected transient failure(s). " +
                              "Check logs for retry warnings and MuonroiMetrics.RetryAttemptCount increments.",
                result.Latitude,
                result.Longitude,
                result.RawJson
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // Payment endpoints
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes a payment through the "muonroi-standard" pipeline.
    /// Set <c>simulateTransientFailure=true</c> in the body to trigger one retry.
    /// </summary>
    [HttpPost("payment")]
    public async Task<IActionResult> ProcessPaymentAsync(
        [FromBody] PaymentRequest request,
        [FromQuery] bool simulateTransientFailure = false,
        CancellationToken ct = default)
    {
        try
        {
            PaymentResult result = await paymentService.ProcessPaymentAsync(
                request, simulateTransientFailure, ct);
            return Ok(result);
        }
        catch (BrokenCircuitException bcx)
        {
            logger.Warn("Circuit open — rejecting payment fast: {Message}", bcx.Message);
            return StatusCode(503, new
            {
                error = "Circuit breaker is open. Payment gateway is temporarily unavailable.",
                detail = bcx.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Triggers a hard (non-transient) failure that increments the circuit-breaker failure counter.
    /// Call this endpoint ≥5 times within 30 seconds to open the circuit.
    /// </summary>
    [HttpPost("payment/hard-failure")]
    public async Task<IActionResult> ProcessPaymentWithHardFailureAsync(
        CancellationToken ct = default)
    {
        try
        {
            PaymentResult result = await paymentService.ProcessPaymentWithHardFailureAsync(ct);
            return Ok(result);
        }
        catch (BrokenCircuitException bcx)
        {
            return StatusCode(503, new
            {
                error = "Circuit breaker OPEN — fast-failing without calling the gateway.",
                detail = bcx.Message,
                hint = "Wait 30 seconds for the circuit to enter half-open state, then retry."
            });
        }
        catch (Exception ex)
        {
            // Each unhandled exception here feeds into the failure-ratio counter.
            return StatusCode(500, new
            {
                error = ex.Message,
                hint = "Keep calling this endpoint to push the failure ratio above 50% and open the circuit."
            });
        }
    }

    // -------------------------------------------------------------------------
    // Self-contained demo endpoints (no external I/O)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Self-contained retry demonstration.
    /// Fails <paramref name="times"/> times then succeeds — no external service required.
    /// The "muonroi-standard" pipeline retries up to 3 times by default.
    /// </summary>
    [HttpGet("demo/retry")]
    public async Task<IActionResult> DemoRetryAsync(
        [FromQuery] int times = 2,
        CancellationToken ct = default)
    {
        int attempt = 0;
        ResiliencePipeline pipeline = pipelineProvider.GetPipeline("muonroi-standard");

        try
        {
            string outcome = await pipeline.ExecuteAsync(async token =>
            {
                attempt++;
                logger.Info("Demo retry — attempt #{Attempt}", attempt);

                if (attempt <= times)
                {
                    // MTransientException is in the retry predicate.
                    throw new MTransientException(
                        $"Demo transient failure on attempt {attempt}/{times}");
                }

                await Task.Delay(10, token);
                return $"Succeeded on attempt #{attempt} after {attempt - 1} retry(ies).";
            }, ct);

            return Ok(new
            {
                description = "Retry demo complete. Check logs for 'Retrying operation' warnings " +
                              "and MuonroiMetrics.RetryAttemptCount telemetry.",
                outcome,
                totalAttempts = attempt,
                retriesUsed = attempt - 1,
                retryAttemptMetricName = "muonroi.retry.attempts"
            });
        }
        catch (MTransientException ex)
        {
            return StatusCode(503, new
            {
                error = $"All retry attempts exhausted. Last error: {ex.Message}",
                hint = $"The pipeline retries up to 3 times. You requested {times} failure(s) — " +
                       "reduce 'times' to ≤3 to observe a successful outcome."
            });
        }
    }

    /// <summary>
    /// Self-contained circuit-breaker demonstration.
    /// Each call throws a generic exception. After ≥5 calls with ≥50% failures in the
    /// 30-second window the circuit opens; subsequent calls fail fast with
    /// <see cref="BrokenCircuitException"/> without executing the delegate.
    /// </summary>
    [HttpGet("demo/circuit-breaker")]
    public async Task<IActionResult> DemoCircuitBreakerAsync(CancellationToken ct = default)
    {
        ResiliencePipeline pipeline = pipelineProvider.GetPipeline("muonroi-standard");

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                logger.Warn("Demo circuit-breaker — throwing generic exception (counts toward failure ratio)");

                // Generic Exception matches the circuit-breaker predicate but NOT the retry predicate,
                // so it propagates immediately and increments the failure-ratio counter.
                await Task.CompletedTask; // satisfy the async contract before throwing
                throw new InvalidOperationException(
                    "Simulated hard failure for circuit-breaker demo.");
            }, ct);

            return Ok("This line is never reached when the exception is thrown.");
        }
        catch (BrokenCircuitException bcx)
        {
            return StatusCode(503, new
            {
                circuitState = "Open",
                description = "Circuit breaker is OPEN. The delegate was not executed — " +
                              "the pipeline short-circuited immediately.",
                detail = bcx.Message,
                hint = "Wait 30 seconds for the circuit to half-open, then call this endpoint once more to close it."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                circuitState = "Closed or HalfOpen",
                error = ex.Message,
                hint = "Call this endpoint ≥5 times within 30 seconds to open the circuit. " +
                       "Circuit opens when failure ratio exceeds 50% over ≥5 throughput samples."
            });
        }
    }
}
