using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Registration;
using Quickstart.Integration.Api.Connectors;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Built-in connectors + DefaultConnectorRegistry
//
// AddMBuiltInConnectors() registers:
//   - HttpConnector          (type="http")
//   - SmtpConnector          (type="email")
//   - SlackWebhookConnector  (type="slack")
//   - SqlQueryConnector      (type="sql")
//   - RedisConnector         (type="redis")
//   - JiraCloudPresetConnector     (type="jira-cloud")
//   - ConfluencePresetConnector    (type="confluence")
//   - GenericRestPresetConnector   (type="rest")
//   - DefaultConnectorRegistry as IConnectorRegistry
//   - Named HttpClient "MuonroiConnector" shared by all HTTP-based connectors
// -------------------------------------------------------------------------
builder.Services.AddMBuiltInConnectors();

// -------------------------------------------------------------------------
// Custom connector — GitHubConnector
//
// Register as IServiceTaskConnector so DefaultConnectorRegistry picks it up
// alongside the built-in connectors (it scans all IServiceTaskConnector
// registrations at startup).
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IServiceTaskConnector, GitHubConnector>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Preserve camelCase output and allow reading JsonDocument from request bodies
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Integration API",
        Version = "v1",
        Description = """
            Demonstrates all features of Muonroi.Integration.Connectors:

            • IConnectorRegistry  — discover and resolve connectors at runtime
            • IServiceTaskConnector — execute any connector via ConnectorContext / ConnectorResult
            • ConnectorMetadata   — display name, category, icon, field schema
            • GetConfigSchema()   — per-connector JSON schema for UI-driven config forms
            • TestConnectionAsync — verify credentials before saving a connector
            • Built-in connectors: http, email, slack, sql, redis, jira-cloud, confluence, rest
            • Custom connector:    github (DevOps category, PAT auth, get-user / list-repos)

            Quick-start:
              1. GET  /api/connectors              → list all registered connectors
              2. GET  /api/connectors/{type}       → inspect schema for one connector
              3. POST /api/connectors/http/execute → live call to JSONPlaceholder (no config needed)
              4. POST /api/connectors/slack/webhook → dry-run unless Connectors:Slack:Enable=true
              5. POST /api/connectors/{type}/test  → test any connector's credentials
              6. POST /api/connectors/{type}/execute → run any connector with a custom config body
            """
    });
});

// -------------------------------------------------------------------------
// Build + middleware pipeline
// -------------------------------------------------------------------------
WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Quickstart.Integration v1");
    c.RoutePrefix = string.Empty; // serve Swagger UI at root
});

app.MapControllers();

// Lightweight health check
app.MapGet("/health", (IConnectorRegistry registry) => Results.Ok(new
{
    Status = "Healthy",
    RegisteredConnectors = registry.ListAvailable().Select(m => m.Type)
}));

app.Run();
