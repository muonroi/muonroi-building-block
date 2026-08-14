WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Core services required by the license pipeline.
// LicenseStore / LicenseVerifier / LicenseActivationService depend on
// IMJsonSerializeService, and the guard pipeline uses IMDateTimeService.
// A full app gets these from Muonroi.Core's AddCoreServices(); the sample
// registers just these two so no Redis/config infrastructure is pulled in.
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();

// -------------------------------------------------------------------------
// Muonroi.Governance — OSS license protection
// AddLicenseProtection() binds LicenseConfigs ("LicenseConfigs" section),
// verifies the license (falling back to Free tier when none is present), and
// registers:
//   LicenseState              → resolved/verified license
//   ILicenseGuard             → LicenseGuard (tier + feature enforcement)
//   ILicenseStore             → LicenseStore
//   ITenantLicenseFeatureGate → TenantLicenseFeatureGateAdapter
// With no license file the app runs in Free tier (LicenseState.CreateFree()).
// -------------------------------------------------------------------------
builder.Services.AddLicenseProtection(builder.Configuration);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Governance API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Governance OSS license protection: " +
                      "ILicenseGuard (Tier, IsFreeMode, HasFeature, EnsureFeature) and " +
                      "the MapMuonroiLicenseInfoEndpoint() license-info endpoint."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// -------------------------------------------------------------------------
// License info endpoint (from Muonroi.Governance) — returns tier + activation
// JWT for frontend license verification. Defaults to /api/v1/license/info.
// -------------------------------------------------------------------------
app.MapMuonroiLicenseInfoEndpoint();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
