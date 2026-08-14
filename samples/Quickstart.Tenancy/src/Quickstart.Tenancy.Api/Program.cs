WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Tenancy + Muonroi.Tenancy.Core
//
// Muonroi.Tenancy.Core provides the runtime building blocks:
//   - TenantContext         : AsyncLocal-backed ITenantContext (also exposes the
//                             static CurrentTenantId used by EF global filters).
//   - DefaultTenantIdResolver: resolves tenant id from claim → header → route →
//                             path → subdomain (ITenantIdResolver).
//   - TenantSchemaSelector  : maps tenant id → schema and rewrites connection
//                             strings for SeparateSchema isolation.
//   - MappingTenantConnectionStringFactory: per-tenant connection-string lookup.
//
// Muonroi.Tenancy adds the ASP.NET integration:
//   - TenantResolutionMiddleware: validates the tenant id format, cross-checks the
//                             header against the tenant claim, sets
//                             TenantContext.CurrentTenantId, and tags the OTel span.
//
// The full DI helper AddTenantContext() additionally enforces a license feature gate
// (it requires AddLicenseProtection first), so this sample registers the individual
// services directly to stay free of external services.
// -------------------------------------------------------------------------

// Bind multi-tenant + per-tenant connection-string options consumed by
// TenantSchemaSelector and MappingTenantConnectionStringFactory.
builder.Services.Configure<MultiTenantOptions>(
    builder.Configuration.GetSection(MultiTenantOptions.SectionName));
builder.Services.Configure<TenantConnectionStringsOptions>(
    builder.Configuration.GetSection(TenantConnectionStringsOptions.SectionName));

// Ambient tenant context (AsyncLocal).
builder.Services.AddScoped<ITenantContext, TenantContext>();

// HTTP-based tenant id resolver.
builder.Services.AddScoped<ITenantIdResolver, DefaultTenantIdResolver>();

// Schema selector for SeparateSchema isolation.
builder.Services.AddSingleton<TenantSchemaSelector>();

// Per-tenant connection-string factory (falls back to the "default" entry).
builder.Services.AddSingleton<ITenantConnectionStringFactory>(sp =>
{
    Microsoft.Extensions.Options.IOptions<TenantConnectionStringsOptions> opts =
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TenantConnectionStringsOptions>>();
    string fallback = builder.Configuration.GetConnectionString("Default")
                      ?? "Host=localhost;Database=quickstart;Username=app;Password=app";
    return new MappingTenantConnectionStringFactory(opts, fallback);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Tenancy API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Tenancy + Muonroi.Tenancy.Core: TenantResolutionMiddleware, " +
                      "ITenantContext (AsyncLocal), DefaultTenantIdResolver, TenantSchemaSelector, and " +
                      "MappingTenantConnectionStringFactory."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// -------------------------------------------------------------------------
// TenantResolutionMiddleware — resolves + validates the tenant id and writes it to
// TenantContext.CurrentTenantId for the duration of the request.
//
// This middleware requires an authenticated user carrying a tenant claim and returns
// 401 (missing/mismatched claim) or 400 (malformed tenant id) otherwise. To keep the
// rest of the sample usable without an auth setup, it is mounted on the "/secure"
// branch only (a real service would call app.UseMiddleware<TenantResolutionMiddleware>()
// in the main pipeline after authentication). The /api/tenant/* endpoints exercise the
// resolver/context/schema/factory services directly and stay reachable.
// -------------------------------------------------------------------------
app.Map("/secure", branch =>
{
    branch.UseMiddleware<TenantResolutionMiddleware>();
    branch.Run(async context =>
        await context.Response.WriteAsJsonAsync(new
        {
            tenantId = TenantContext.CurrentTenantId,
            message = "Tenant resolved by TenantResolutionMiddleware."
        }));
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
