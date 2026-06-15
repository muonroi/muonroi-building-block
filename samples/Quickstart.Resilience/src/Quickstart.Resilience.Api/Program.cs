using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging;
using Muonroi.Resilience;
using Polly;
using Polly.Retry;
using Quickstart.Resilience.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Logging — IMLog<T> available via IMLog<WeatherApiClient>, IMLog<PaymentService>, etc.
// -------------------------------------------------------------------------
builder.Logging.AddMuonroiLogging();

// -------------------------------------------------------------------------
// Standard Muonroi resilience pipeline
//
// Registers the named pipeline "muonroi-standard" with:
//   • Retry        — up to 3 attempts, exponential back-off + jitter, 1 s base delay
//                    handles: MTransientException, HttpRequestException
//                    OnRetry: logs warning + increments MuonroiMetrics.RetryAttemptCount
//   • CircuitBreaker — opens when ≥50% of calls fail over a 30 s / ≥5 throughput window
//                      stays open for 30 s, handles: any Exception
//   • Timeout      — 10 s hard timeout per execution
// -------------------------------------------------------------------------
builder.Services.AddMuonroiResilience();

// -------------------------------------------------------------------------
// Custom pipeline — "payment-gateway"
//
// Shows how to register a bespoke pipeline alongside "muonroi-standard".
// Payment workloads often tolerate more retries and need a longer timeout.
// -------------------------------------------------------------------------
builder.Services.AddResiliencePipeline("payment-gateway", (pipelineBuilder, context) =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<MTransientException>()
                .Handle<HttpRequestException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(2),
            OnRetry = args =>
            {
                ILogger logger = context.ServiceProvider
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning(
                    "[payment-gateway] Retry attempt {Attempt} due to: {Exception}",
                    args.AttemptNumber, args.Outcome.Exception?.Message);
                return default;
            }
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});

// -------------------------------------------------------------------------
// HttpClient — named client used by WeatherApiClient
// -------------------------------------------------------------------------
builder.Services.AddHttpClient("weather-api", client =>
{
    string baseUrl = builder.Configuration["WeatherApi:BaseUrl"]
                    ?? "https://api.open-meteo.com/v1";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15); // outer safety net; pipeline adds 10 s inner timeout
});

// -------------------------------------------------------------------------
// Application services
// -------------------------------------------------------------------------
builder.Services.AddSingleton<WeatherApiClient>();
builder.Services.AddSingleton<PaymentService>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Resilience API",
        Version = "v1",
        Description =
            "Demonstrates all Muonroi.Resilience features:\n\n" +
            "• AddMuonroiResilience() — registers 'muonroi-standard' pipeline (retry + circuit breaker + timeout)\n" +
            "• Custom pipeline registration alongside 'muonroi-standard'\n" +
            "• ResiliencePipelineProvider<string> injection and GetPipeline(name)\n" +
            "• pipeline.ExecuteAsync(async ct => { ... }) execution pattern\n" +
            "• MTransientException → retry trigger\n" +
            "• Generic Exception  → circuit-breaker failure counter\n" +
            "• BrokenCircuitException → fast-fail when circuit is open\n" +
            "• MuonroiMetrics.RetryAttemptCount (muonroi.retry.attempts) OTel counter\n" +
            "• PolicyHandler for building typed ResiliencePipeline<T> instances\n\n" +
            "Start with GET /api/resilience/pipeline/info to see all registered pipelines, " +
            "then try the /demo/retry and /demo/circuit-breaker endpoints."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Quickstart.Resilience v1");
    options.RoutePrefix = string.Empty; // serve Swagger UI at root
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", pipelines = new[] { "muonroi-standard", "payment-gateway" } }));

app.Run();
