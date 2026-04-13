using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Muonroi.BuildingBlock.Shared.License;
using Muonroi.BuildingBlock.Shared.Policy;

if (args.Length < 2)
{
    Console.WriteLine("Usage: PolicySigner <private_key_path> <license_id> [output_path]");
    return;
}

var privateKeyPath = args[0];
var licenseId = args[1];
var outputPath = args.Length > 2 ? args[2] : "policy.json";

if (!File.Exists(privateKeyPath))
{
    Console.WriteLine($"Error: Private key file not found at {privateKeyPath}");
    return;
}

try
{
    var privateKeyPem = File.ReadAllText(privateKeyPath);
    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem.ToCharArray());

    var policy = new LicensePolicy
    {
        PolicyId = "pol_" + Guid.NewGuid().ToString("N").Substring(0, 8),
        Version = "1.0.0",
        LicenseId = licenseId,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
        Enforcement = new PolicyEnforcementRules
        {
            EnforceOnDatabase = true,
            EnableAntiTampering = true,
            FailMode = LicenseFailMode.Hard,
            MaxApiRequestsPerMinute = 10,
            MaxDbOperationsPerMinute = 5
        },
        FeatureQuotas = new Dictionary<string, FeatureQuota>
        {
            { "api.create", new FeatureQuota { MaxUsagePerDay = 1000 } },
            { "db.save", new FeatureQuota { MaxUsagePerDay = 5000 } }
        }
    };

    // Data to sign (must match PolicyVerifier.cs)
    var signingData = new
    {
        policy.PolicyId,
        policy.Version,
        policy.LicenseId,
        policy.IssuedAt,
        policy.ExpiresAt,
        policy.Enforcement,
        policy.FeatureQuotas
    };

    var jsonToSign = JsonSerializer.Serialize(signingData);
    var signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(jsonToSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    policy.Signature = Convert.ToBase64String(signatureBytes);

    var finalJson = JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outputPath, finalJson);

    Console.WriteLine($"Successfully generated signed policy file: {outputPath}");
    Console.WriteLine($"Policy ID: {policy.PolicyId}");
    Console.WriteLine($"License ID: {policy.LicenseId}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
