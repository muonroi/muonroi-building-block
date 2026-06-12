using Muonroi.Governance.License;
using Muonroi.RuleEngine.Core;
using Muonroi.RuleEngine.Runtime.Rules;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new LicenseState
{
    IsValid = true,
    Tier = LicenseTier.Enterprise,
    Payload = new LicensePayload
    {
        LicenseId = "sample-multitenant",
        AllowedFeatures = [FreeTierFeatures.Premium.RuleEngine]
    }
});

builder.Services.AddRuleEngine<MultiTenant.Api.Models.PricingRequest>();
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);

bool useControlPlane = builder.Configuration.GetValue<bool>("ControlPlane:Enabled");
if (useControlPlane)
{
    string connectionString = builder.Configuration.GetConnectionString("RuleEngineDb")
        ?? throw new InvalidOperationException("ConnectionStrings:RuleEngineDb is required when ControlPlane:Enabled=true.");

    builder.Services.AddMRuleEngineWithPostgres(
        connectionString,
        options => builder.Configuration.GetSection("RuleControlPlane").Bind(options));

    string? redisConnection = builder.Configuration.GetConnectionString("RuleEngineRedis");
    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        builder.Services.AddMRuleEngineWithRedisHotReload(redisConnection);
    }
}

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
