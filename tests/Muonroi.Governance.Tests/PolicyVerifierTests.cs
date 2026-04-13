using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Tests;

public sealed class PolicyVerifierTests
{
    [Fact]
    public void Verify_Returns_False_When_Policy_Is_Null()
    {
        PolicyVerifier verifier = CreateVerifier();

        verifier.Verify(null!).Should().BeFalse();
    }

    [Fact]
    public void Verify_Returns_False_When_Signature_Is_Missing()
    {
        PolicyVerifier verifier = CreateVerifier();

        verifier.Verify(new LicensePolicy { PolicyId = "POL-1" }).Should().BeFalse();
    }

    [Fact]
    public void Verify_Returns_False_When_Public_Key_File_Is_Missing()
    {
        using TempDir temp = new();
        PolicyVerifier verifier = CreateVerifier(
            new LicenseConfigs { PublicKeyPath = "keys/public.pem" },
            temp.Path);

        verifier.Verify(new LicensePolicy { PolicyId = "POL-1", Signature = Convert.ToBase64String([1, 2, 3]) })
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Verify_Returns_True_For_Valid_Signature()
    {
        using TempDir temp = new();
        using RSA rsa = RSA.Create(2048);
        File.WriteAllText(Path.Combine(temp.Path, "public.pem"), rsa.ExportRSAPublicKeyPem());
        LicensePolicy policy = CreateSignedPolicy(rsa, new MJsonSerializeService(), expiresAt: DateTimeOffset.UtcNow.AddDays(1));
        PolicyVerifier verifier = CreateVerifier(new LicenseConfigs { PublicKeyPath = "public.pem" }, temp.Path);

        verifier.Verify(policy).Should().BeTrue();
    }

    [Fact]
    public void Verify_Returns_False_For_Expired_Policy_Even_When_Signature_Is_Valid()
    {
        using TempDir temp = new();
        using RSA rsa = RSA.Create(2048);
        File.WriteAllText(Path.Combine(temp.Path, "public.pem"), rsa.ExportRSAPublicKeyPem());
        LicensePolicy policy = CreateSignedPolicy(rsa, new MJsonSerializeService(), expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        PolicyVerifier verifier = CreateVerifier(new LicenseConfigs { PublicKeyPath = "public.pem" }, temp.Path);

        verifier.Verify(policy).Should().BeFalse();
    }

    [Fact]
    public void Verify_Returns_False_When_Signature_Is_Invalid_Base64()
    {
        using TempDir temp = new();
        File.WriteAllText(Path.Combine(temp.Path, "public.pem"), RSA.Create(2048).ExportRSAPublicKeyPem());
        PolicyVerifier verifier = CreateVerifier(new LicenseConfigs { PublicKeyPath = "public.pem" }, temp.Path);
        LicensePolicy policy = new() { PolicyId = "POL-ERR", Signature = "not-base64" };

        verifier.Verify(policy).Should().BeFalse();
    }

    private static PolicyVerifier CreateVerifier(LicenseConfigs? configs = null, string? contentRootPath = null, IMJsonSerializeService? json = null)
    {
        IHostEnvironment? environment = null;
        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            environment = Substitute.For<IHostEnvironment>();
            environment.ContentRootPath.Returns(contentRootPath);
        }

        return new PolicyVerifier(configs ?? new LicenseConfigs(), environment, json ?? new MJsonSerializeService());
    }

    private static LicensePolicy CreateSignedPolicy(RSA rsa, IMJsonSerializeService json, DateTimeOffset? expiresAt)
    {
        LicensePolicy policy = new()
        {
            PolicyId = "POL-VALID",
            Version = "1.2.3",
            LicenseId = "LIC-123",
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = expiresAt,
            Enforcement = new PolicyEnforcementRules
            {
                EnforceOnDatabase = true,
                EnableAntiTampering = true,
                MaxApiRequestsPerMinute = 500
            },
            FeatureQuotas = new Dictionary<string, FeatureQuota>
            {
                ["reports"] = new() { MaxUsagePerDay = 100, MaxConcurrentUsage = 5 }
            }
        };

        string serialized = json.Serialize(new
        {
            policy.PolicyId,
            policy.Version,
            policy.LicenseId,
            policy.IssuedAt,
            policy.ExpiresAt,
            policy.Enforcement,
            policy.FeatureQuotas
        });
        byte[] data = Encoding.UTF8.GetBytes(serialized);
        byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        policy.Signature = Convert.ToBase64String(signature);
        return policy;
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "muonroi-governance-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
