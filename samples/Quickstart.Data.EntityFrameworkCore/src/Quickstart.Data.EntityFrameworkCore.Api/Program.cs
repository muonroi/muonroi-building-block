WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Data.EntityFrameworkCore
//
// The package's primary public registration is
//   services.AddDbContextConfigure<TDbContext, TPermission>(configuration)
// which binds DatabaseConfigs (DbType + connection string), selects the matching
// IDbContextConfigurator (SqlServer/PostgreSql/MySql/Sqlite/Mongo), registers the
// license SaveChanges interceptor, tenant context, permission sync, and the auth
// repositories. That path REQUIRES a real database provider + connection string,
// so it is not used in this no-database sample.
//
// Instead we register a concrete MDbContext subclass (SampleNotesDbContext) with
// the EF Core in-memory provider — referenced transitively by the package — to
// demonstrate MDbContext's audit timestamping and soft-delete behaviour without
// any external service. To enable the full configurator path in a real service,
// replace UseInMemoryDatabase with the provider implied by DatabaseConfigs and
// call AddDbContextConfigure<SampleNotesDbContext, YourPermissionEnum>(config).
// -------------------------------------------------------------------------
builder.Services.AddDbContext<SampleNotesDbContext>(options =>
    options.UseInMemoryDatabase("quickstart-notes"));

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Data.EntityFrameworkCore API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Data.EntityFrameworkCore: an MDbContext subclass with " +
                      "automatic audit timestamping (CreationTime/CreatorUserId) and soft-delete " +
                      "(DELETE becomes UPDATE IsDeleted=true, filtered out of queries)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
