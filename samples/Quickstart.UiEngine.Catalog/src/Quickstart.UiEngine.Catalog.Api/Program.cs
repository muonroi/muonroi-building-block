WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Logging — supplies IMLog<> required by CatalogScanService.
// -------------------------------------------------------------------------
builder.Logging.AddMuonroiLogging();

// -------------------------------------------------------------------------
// UI engine catalog  (Muonroi.UiEngine.Catalog)
//
// AddUiEngineCatalog(configure) registers the catalog scan service plus a
// snapshot store. With no Postgres/SqlServer connection string configured it
// uses InMemoryCatalogSnapshotStore — so the sample needs no database.
// Set PostgresConnectionString / SqlServerConnectionString on the options to
// switch to EfCoreCatalogSnapshotStore (adds the catalog DB migrator).
// -------------------------------------------------------------------------
builder.Services.AddUiEngineCatalog(options =>
{
    // Leave connection strings empty -> in-memory snapshot store (no DB).
    options.AutoMigrateDatabase = false;
});

// -------------------------------------------------------------------------
// MVC controllers + the package's catalog controllers
//
// The catalog controllers ship inside Muonroi.UiEngine.Catalog, so register
// that assembly as an application part. UiEngineCatalogController scans the API
// surface via IApiDescriptionGroupCollectionProvider (added by AddControllers +
// AddEndpointsApiExplorer).
// -------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddApplicationPart(typeof(UiEngineCatalogController).Assembly);

// UiEngineCatalogController is [Authorize]; register a minimal authentication
// scheme + a permissive default policy so the sample is explorable. (The sibling
// MConnectorCatalogController is [AllowAnonymous] and works regardless.)
builder.Services.AddAuthentication("Sample")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SampleAuthHandler>("Sample", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.UiEngine.Catalog API",
        Version = "v1",
        Description = "Demonstrates Muonroi.UiEngine.Catalog: AddUiEngineCatalog() wires the " +
                      "CatalogScanService and (in-memory by default) snapshot store. The package " +
                      "controllers — UiEngineCatalogController (apis/rules/bindings/graph/snapshots) " +
                      "and MConnectorCatalogController — are registered via AddApplicationPart. " +
                      "Snapshots persist to Postgres/SqlServer when a connection string is supplied."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
