using Muonroi.Integration.Connectors.Registration;
using Muonroi.RuleEngine.Proliferation;
using Muonroi.RuleEngine.Proliferation.Persistence;
using Quickstart.RuleEngine.Proliferation.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Supporting dependencies for the proliferation engine
//
// AddMBuiltInConnectors() supplies IServiceTaskConnector, which the engine's
// ExternalScenarioExecutor depends on. The standalone config provider routes
// every scenario to the internal executor (no control-plane present).
// -------------------------------------------------------------------------
builder.Services.AddMBuiltInConnectors();
builder.Services.AddSingleton<IExternalProjectConfigProvider, StandaloneExternalProjectConfigProvider>();

// -------------------------------------------------------------------------
// Proliferation engine  (Muonroi.RuleEngine.Proliferation)
//
// AddMProliferationEngine(configuration) registers the AI scenario-generation
// stack: the brain provider (Ollama / OpenAI / Claude per ProliferationOptions),
// prompt builder, deduplicators, budget allocator, scenario executors, and the
// ProliferationWorker hosted service. Defaults to an in-memory IProliferationStore.
//
// Options are bound from the "Proliferation" section. The brain only calls an
// external AI endpoint when a generation runs; registration touches no network.
// -------------------------------------------------------------------------
builder.Services.AddMProliferationEngine(builder.Configuration);

// -------------------------------------------------------------------------
// Postgres persistence  (Muonroi.RuleEngine.Proliferation.Persistence)
//
// AddMProliferationPostgres(connectionString) registers ProliferationDbContext
// and replaces the in-memory IProliferationStore with PostgresProliferationStore.
// The connection string only configures the DbContext; the ProliferationWorker
// queries the store at runtime and therefore needs a reachable Postgres instance.
// -------------------------------------------------------------------------
string connectionString = builder.Configuration.GetConnectionString("ProliferationDb")
    ?? "Host=localhost;Port=5432;Database=muonroi_proliferation;Username=admin;Password=admin";
builder.Services.AddMProliferationPostgres(connectionString);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.Proliferation API",
        Version = "v1",
        Description = "Demonstrates Muonroi.RuleEngine.Proliferation + .Persistence: " +
                      "AddMProliferationEngine() wires the AI neuron-scenario brain, executors, " +
                      "and ProliferationWorker; AddMProliferationPostgres() swaps the in-memory " +
                      "store for PostgresProliferationStore. The StatsController reads aggregate " +
                      "proliferation statistics via IProliferationStore (requires Postgres)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
