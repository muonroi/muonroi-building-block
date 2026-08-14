using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.CEP;
using Quickstart.CEP.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── CEP web services ──────────────────────────────────────────────────────────
// AddCepWeb() registers:
//   ICepConfigRepository  → InMemoryCepConfigRepository (no DB connection string provided)
//   IMDateTimeService, IMJsonSerializeService, ISystemExecutionContextAccessor
//   CepController (from the CEP assembly) is added via AddApplicationPart
//
// To switch to a real database, pass a configure action:
//   builder.Services.AddCepWeb(opts => opts.PostgresConnectionString = "Host=...");
//   builder.Services.AddCepWeb(opts => opts.SqlServerConnectionString = "Server=...");
builder.Services.AddCepWeb();

// ── Execution context ─────────────────────────────────────────────────────────
// ISystemExecutionContextAccessor propagates tenant / user / correlation data
// across the async call tree via AsyncLocal.
// AddCepWeb registers it with TryAddSingleton, but registering it here as well
// is safe (the second registration is a no-op) and makes the dependency explicit.
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// ── Application services ──────────────────────────────────────────────────────
// TemperatureAlertService is a singleton because it owns in-memory CepWindow<T>
// instances that accumulate events across requests.  Making it transient or scoped
// would reset the windows on every request.
builder.Services.AddSingleton<TemperatureAlertService>();

// ── ASP.NET / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.CEP API",
        Version = "v1",
        Description =
            "Demonstrates all features of Muonroi.RuleEngine.CEP (Complex Event Processing):\n" +
            "• CepWindowBuilder — fluent CepConfig construction (Named, ForTenant, Sliding, Tumbling, CorrelateBy, WithMetadata)\n" +
            "• CepWindowRuntimeBuilder<T> — typed runtime window from a persisted CepConfig\n" +
            "• CepWindow<T> — Add(payload, timestamp) with automatic key selection\n" +
            "• CepEngine<T> — low-level AddEvent(key, value, timestamp) with sliding/tumbling semantics\n" +
            "• ICepConfigRepository — List, Get, Save, Delete for window configs (in-memory store)\n" +
            "• WindowType — Sliding and Tumbling windowing strategies\n" +
            "• Anomaly detection demo — alert-demo endpoint shows end-to-end CEP pattern"
    });
});

WebApplication app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// Populate ISystemExecutionContextAccessor from incoming request headers so that
// any CEP components that inspect tenant/correlation context have data to work with.
app.Use(async (context, next) =>
{
    ISystemExecutionContextAccessor accessor =
        context.RequestServices.GetRequiredService<ISystemExecutionContextAccessor>();

    string? tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    string? userId   = context.Request.Headers["X-User-Id"].FirstOrDefault();

    accessor.Set(new SystemExecutionContext(
        tenantId: tenantId,
        userId: userId,
        username: null,
        correlationId: context.TraceIdentifier,
        accessToken: null,
        apiKey: null,
        isAuthenticated: userId is not null,
        permissions: [],
        sourceType: "http"));

    try
    {
        await next();
    }
    finally
    {
        accessor.Clear();
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Quickstart.CEP" }));

 await app.RunAsync();
