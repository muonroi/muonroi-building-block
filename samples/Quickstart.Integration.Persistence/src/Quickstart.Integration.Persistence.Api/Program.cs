using Muonroi.Integration.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Data Protection — required by EfConnectorCredentialStore
//
// Connector credentials are encrypted at rest with ASP.NET Data Protection,
// using a per-tenant protector (CreateProtector("connector-creds:{tenantId}")).
// AddDataProtection() supplies the IDataProtectionProvider it depends on.
// -------------------------------------------------------------------------
builder.Services.AddDataProtection();

// -------------------------------------------------------------------------
// Connector persistence  (Muonroi.Integration.Persistence)
//
// AddMConnectorPersistence(connectionString) registers:
//   - ConnectorDbContext        (Npgsql)
//   - EfConnectorConfigStore     as IConnectorConfigStore
//   - EfConnectorCredentialStore as IConnectorCredentialStore (encrypted)
//
// The connection string only configures the DbContext; no connection opens at
// registration time, so the app starts without a live Postgres instance.
// Store operations require a reachable database.
// -------------------------------------------------------------------------
string connectionString = builder.Configuration.GetConnectionString("ConnectorDb")
    ?? "Host=localhost;Port=5432;Database=muonroi_connectors;Username=admin;Password=admin";
builder.Services.AddMConnectorPersistence(connectionString);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Integration.Persistence API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Integration.Persistence: " +
                      "AddMConnectorPersistence() wires ConnectorDbContext plus " +
                      "EfConnectorConfigStore (IConnectorConfigStore) and the encrypted " +
                      "EfConnectorCredentialStore (IConnectorCredentialStore). " +
                      "The ConnectorsController performs CRUD on connector configs and " +
                      "stores/reads encrypted credentials (requires Postgres)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
