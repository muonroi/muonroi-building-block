






namespace Quickstart.Resilience.Api.Controllers;

/// <summary>
/// Provides human-readable descriptions of every resilience pipeline registered in the DI container.
///
/// Endpoints:
/// <list type="bullet">
///   <item>GET /api/resilience/pipeline/info — describes all named pipelines and their settings</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/resilience/pipeline")]
public sealed class PipelineInfoController(
    ResiliencePipelineProvider<string> pipelineProvider) : ControllerBase
{
    /// <summary>
    /// Returns a description of each named pipeline registered in this application, including
    /// the strategy parameters used when Polly built the pipeline.
    ///
    /// The settings listed here mirror what is configured in <c>Program.cs</c>:
    /// <list type="bullet">
    ///   <item><c>muonroi-standard</c> — registered by <c>AddMuonroiResilience()</c></item>
    ///   <item><c>payment-gateway</c>  — registered inline in Program.cs as a custom example</item>
    /// </list>
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetPipelineInfo()
    {
        // Resolve both pipelines to confirm they are registered and healthy.
        ResiliencePipeline muonroiStandard = pipelineProvider.GetPipeline("muonroi-standard");
        ResiliencePipeline paymentGateway = pipelineProvider.GetPipeline("payment-gateway");

        return Ok(new
        {
            note = "Polly does not expose its internal strategy graph at runtime. " +
                   "The settings below are the values configured in Program.cs and AddMuonroiResilience().",
            pipelines = new object[]
            {
                new
                {
                    name = "muonroi-standard",
                    registeredBy = "services.AddMuonroiResilience()",
                    resolved = muonroiStandard is not null,
                    strategies = new object[]
                    {
                        new
                        {
                            type = "Retry",
                            maxRetryAttempts = 3,
                            backoffType = "Exponential",
                            useJitter = true,
                            baseDelay = "1 second",
                            shouldHandle = new[] { "MTransientException", "HttpRequestException" },
                            onRetry = "Logs warning + increments MuonroiMetrics.RetryAttemptCount (muonroi.retry.attempts)"
                        },
                        new
                        {
                            type = "CircuitBreaker",
                            failureRatio = "50%",
                            samplingDuration = "30 seconds",
                            minimumThroughput = 5,
                            breakDuration = "30 seconds",
                            shouldHandle = new[] { "Any Exception" }
                        },
                        new
                        {
                            type = "Timeout",
                            timeout = "10 seconds"
                        }
                    },
                    executionPattern = "pipeline.ExecuteAsync(async ct => { /* your work */ }, cancellationToken)"
                },
                new
                {
                    name = "payment-gateway",
                    registeredBy = "services.AddResiliencePipeline(\"payment-gateway\", builder => { ... }) in Program.cs",
                    resolved = paymentGateway is not null,
                    strategies = new object[]
                    {
                        new
                        {
                            type = "Retry",
                            maxRetryAttempts = 5,
                            backoffType = "Exponential",
                            useJitter = true,
                            baseDelay = "2 seconds",
                            shouldHandle = new[] { "MTransientException", "HttpRequestException" }
                        },
                        new
                        {
                            type = "Timeout",
                            timeout = "30 seconds"
                        }
                    },
                    note = "Custom pipeline — more retries and a longer timeout suit payment workloads."
                }
            },
            metrics = new
            {
                meterName = "Muonroi.Ecosystem.Core",
                counters = new object[]
                {
                    new
                    {
                        name = "muonroi.retry.attempts",
                        description = "Incremented by OnRetry in 'muonroi-standard'. Tagged with exception.type.",
                        csharpProperty = "MuonroiMetrics.RetryAttemptCount"
                    },
                    new
                    {
                        name = "muonroi.exception.total",
                        description = "Counts all MException-derived exceptions by category and error code.",
                        csharpProperty = "MuonroiMetrics.ExceptionCount"
                    }
                }
            }
        });
    }
}
