using Muonroi.Data.Dapper.Dapper.Handlers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Data.Dapper
//
// The package provides:
//   - MConnectionStringProvider : binds "<name>:ConnectionString" from config.
//   - MDapperCommand            : a tenant-aware command wrapper (CommandText +
//                                 DynamicParameters) consumed by MDapperExtensions.
//   - MSqlMapperTypeExtensions  : registers custom Dapper type handlers
//                                 (Protobuf Timestamp + trimmed strings).
//   - DapperRlsBypass           : ambient cross-tenant bypass scope for RLS.
//   - AddMuonroiDapperRls()     : optional Row-Level-Security override of IDapper.
//
// AddMuonroiDapperRls is the package's primary registration extension, but it
// only does work when MultiTenantConfigs.EnableRowLevelSecurity is true — in
// which case it swaps the live IDapper for TenantRlsDapper<TConn> and registers
// a hosted startup verifier that connects to the database. This sample keeps RLS
// DISABLED (the default), so the call is a no-op zero-impact early return and the
// app compiles and runs with NO database. See DapperController for the in-process
// APIs (MDapperCommand, type handlers, bypass scope) exercised without a DB.
// -------------------------------------------------------------------------

// Registers the custom Dapper SqlMapper type handlers globally (process-wide,
// no database connection required).
MSqlMapperTypeExtensions.RegisterDapperHandlers();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Data.Dapper API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Data.Dapper building blocks that run without a live " +
                      "database: MDapperCommand, MSqlMapperTypeExtensions.RegisterDapperHandlers(), " +
                      "DapperRlsBypass cross-tenant scope, and the DapperRls provider/guarantee model."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
