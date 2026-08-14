using Muonroi.RuleEngine.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Postgres-backed ruleset control plane
//
// AddMRuleEngineWithPostgres(connectionString) from
// Muonroi.RuleEngine.EntityFrameworkCore registers:
//   - RuleEngineDbContext  (Npgsql, with optional tenant RLS interceptor)
//   - PostgresRuleSetStore       as IRuleSetStore   (replaces the file store)
//   - PostgresRuleSetAuditStore  as IRuleSetAuditStore
//   - IRuleSetApprovalService / ICanaryRolloutService
//   - RulesEngineService (scoped) for ruleset execution + validation
//
// The connection string is only used to configure the DbContext; no database
// connection is opened at registration time, so the app builds and starts
// without a live Postgres instance. Calls that actually query the store
// (e.g. saving a ruleset) require a reachable database.
//
// AddMRuleEngineWithPostgres also calls EnsureFeatureOrThrow(RuleEngine),
// a Premium-tier feature gate enforced at registration.
// -------------------------------------------------------------------------
string connectionString = builder.Configuration.GetConnectionString("RuleDb")
    ?? "Host=localhost;Port=5432;Database=muonroi_rules;Username=admin;Password=admin";

builder.Services.AddMRuleEngineWithPostgres(
    connectionString,
    options =>
    {
        // Demonstrate opting into the maker-checker approval gate.
        options.RequireApproval = false;
        options.EnableCanary = true;
    });

// Enable the approval workflow + canary rollout helper services explicitly.
builder.Services.AddMRuleEngineApprovalWorkflow();
builder.Services.AddMCanaryRollout();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.EntityFrameworkCore API",
        Version = "v1",
        Description = "Demonstrates Muonroi.RuleEngine.EntityFrameworkCore: " +
                      "AddMRuleEngineWithPostgres() swaps the file-backed ruleset store for " +
                      "PostgresRuleSetStore + PostgresRuleSetAuditStore and registers " +
                      "RulesEngineService, approval, and canary services. " +
                      "Configure the RuleDb connection string in appsettings.json; " +
                      "store operations require a reachable Postgres instance."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
