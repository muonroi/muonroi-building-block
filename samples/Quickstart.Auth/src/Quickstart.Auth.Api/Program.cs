using Muonroi.Auth;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Helpers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// IMDateTimeService — required by JwtService for issuing/validating tokens.
// In a full app this is registered by Muonroi.Core's AddCoreServices(); the
// sample registers only the single dependency JwtService needs so no Redis or
// other infrastructure is pulled in.
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();

// -------------------------------------------------------------------------
// MTokenInfo — JWT issuer/audience. Bound from the "TokenConfigs" section
// (MTokenInfo.SectionName) in appsettings.json. Registered explicitly so
// JwtService resolves it from DI instead of re-binding configuration.
// -------------------------------------------------------------------------
MTokenInfo tokenInfo = new();
builder.Configuration.GetSection(tokenInfo.SectionName).Bind(tokenInfo);
builder.Services.AddSingleton(tokenInfo);

// -------------------------------------------------------------------------
// Muonroi.Auth — in-memory RSA key store
// AddInMemoryRsaKeyStore() registers:
//   IRsaKeyStore           → InMemoryRsaKeyStore (generates + rotates RSA keys)
//   ITokenRevocationStore  → TokenRevocationStore (in-process revocation list)
//   IPasswordHasher        → BCryptPasswordHasher
//   JwtService             → RS256 token issue/validate/revoke + JWKS export
// No external dependency is required. Use AddRedisRsaKeyStore(configuration)
// to back the key store + revocation list with Redis instead.
// -------------------------------------------------------------------------
builder.Services.AddInMemoryRsaKeyStore();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Auth API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Auth: RS256 JWT issuance/validation via JwtService, " +
                      "token revocation (ITokenRevocationStore), JWKS export, RSA key rotation, " +
                      "and BCrypt password hashing (MPasswordHelper / IPasswordHasher)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
