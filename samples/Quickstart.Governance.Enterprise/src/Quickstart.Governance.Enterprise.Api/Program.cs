using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.Governance.Enterprise;
using Muonroi.Governance.Operations;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Core services required by the governance pipeline.
//   IMJsonSerializeService          → license/chain serialization
//   IMDateTimeService               → PolicyEnforcer + guard timing
//   ISystemExecutionContextAccessor → tenant/user context for the enhancer
// A full app gets these from AddCoreServices(); registered individually here.
// IHttpClientFactory is required by LicenseActivator (resolved by the
// enterprise activation hosted service at startup).
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddHttpClient();

// -------------------------------------------------------------------------
// Muonroi.Governance.Enterprise — enterprise governance
// AddMEnterpriseGovernance() calls AddLicenseProtection() internally, then
// upgrades the OSS no-op services to enterprise implementations:
//   ILicenseGuardEnhancer  → EnterpriseLicenseGuardEnhancer (anti-tamper + policy)
//   IFingerprintChainStore → FileFingerprintChainStore (signed audit chain)
//   IFingerprintSigner     → HmacFingerprintSigner
//   ILicenseFingerprintProvider → FingerprintProvider (machine + project bound)
// and registers operations services:
//   IMUpgradeCompatibilityService, IMEnterpriseSloPresetService,
//   IMComplianceExportService, IMComplianceEvidencePackService.
// It also wires OpenTelemetry tracing/metrics for anti-tamper + audit sources.
// ILicenseGuard remains available (from AddLicenseProtection) and runs in
// Free tier when no license file is present.
// -------------------------------------------------------------------------
builder.Services.AddMEnterpriseGovernance(builder.Configuration);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Governance.Enterprise API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Governance.Enterprise: AddMEnterpriseGovernance(), " +
                      "ILicenseGuard tier/feature enforcement, and the enterprise operations " +
                      "endpoints (upgrade compatibility + SLO presets) via MapMEnterpriseOperationsEndpoints()."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// -------------------------------------------------------------------------
// Enterprise operations endpoints (from Muonroi.Governance.Enterprise):
//   POST /api/v1/enterprise-ops/upgrade/compatibility/check
//   GET  /api/v1/enterprise-ops/slo/presets
//   GET  /api/v1/enterprise-ops/slo/presets/{presetName}
// Backed by IMUpgradeCompatibilityService + IMEnterpriseSloPresetService.
// -------------------------------------------------------------------------
app.MapMEnterpriseOperationsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
