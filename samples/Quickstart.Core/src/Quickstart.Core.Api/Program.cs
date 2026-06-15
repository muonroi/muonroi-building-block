using Muonroi.Core.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi core services
// AddCoreServices() registers the foundational singletons used across the stack:
//   - IMJsonSerializeService        (System.Text.Json wrapper)
//   - IMDateTimeService             (testable clock: Now/UtcNow/Today/timestamps)
//   - ISystemExecutionContextAccessor (ambient tenant/user/correlation context)
//   - IContextResolver / ITenantContextPolicy
//   - Muonroi structured logging (via AddMuonroiLogging)
//   - Redis / pagination / JWT config binding
// See src/Muonroi.Core/Extensions/CoreServiceCollectionExtensions.cs:24
//
// Parameters:
//   isSecretDefault: true  -> config values are read as-is (no decryption)
//   secretKey: ""          -> no decryption key needed in this sample
//   paginationConfigs/tokenConfig: null -> bound from configuration with defaults
// -------------------------------------------------------------------------
builder.Services.AddCoreServices(
    builder.Configuration,
    isSecretDefault: true,
    secretKey: string.Empty,
    paginationConfigs: null,
    tokenConfig: null);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Core API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Core package: IMDateTimeService (clock), " +
                      "IMJsonSerializeService (JSON round-trip), and " +
                      "ISystemExecutionContextAccessor (ambient tenant/user/correlation context)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
