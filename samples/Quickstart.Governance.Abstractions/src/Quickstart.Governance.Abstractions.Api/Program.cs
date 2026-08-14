var builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
// We register in-memory mocks of the abstractions to demonstrate usage
builder.Services.AddSingleton<ILicenseStore, InMemoryLicenseStore>();
builder.Services.AddSingleton<ILicenseGuard, DemoLicenseGuard>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Governance.Abstractions API",
        Version = "v1",
        Description = "Demonstrates Governance Abstractions capabilities."
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();

// Simple mock implementation of ILicenseStore
public class InMemoryLicenseStore : ILicenseStore
{
    private LicensePayload? _payload;
    private ActivationProof? _proof;
    private string? _jwt;

    public LicensePayload? Load() => _payload;
    public void Save(LicensePayload payload) => _payload = payload;
    public ActivationProof? LoadActivationProof() => _proof;
    public void SaveActivationProof(ActivationProof proof) => _proof = proof;
    public string? LoadActivationJwt() => _jwt;
    public void SaveActivationJwt(string jwt) => _jwt = jwt;
}

// Simple mock implementation of ILicenseGuard
public class DemoLicenseGuard : ILicenseGuard
{
    public LicenseState Current => new LicenseState(); // Assuming a default exists
    public LicenseTier Tier => LicenseTier.Enterprise; // Mocking as Enterprise
    public bool IsFreeMode => Tier == LicenseTier.Free;

    public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null, string? correlationId = null)
    {
        // Valid for demo
    }

    public bool HasFeature(string featureName)
    {
        return Tier == LicenseTier.Enterprise || featureName == "BasicFeature";
    }

    public void EnsureFeature(string featureName)
    {
        if (!HasFeature(featureName))
        {
            throw new LicenseException($"Feature {featureName} is not available in the current license tier.");
        }
    }

    public void RecordAction(LicenseActionContext context)
    {
        // No-op for demo
    }

    public string GetChainToken() => "demo-chain-token";

    public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
    {
        return decryptor(purpose, encryptedData);
    }
}
