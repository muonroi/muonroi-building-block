using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Tests;

public sealed class LicenseStoreTests
{
    [Fact]
    public void Load_Returns_Payload_From_License_File_When_LicenseId_Is_Present()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        LicensePayload expected = new() { LicenseId = "LIC-001", TenantId = "tenant-a" };
        string licensePath = Path.Combine(temp.Path, "licenses", "license.json");
        Directory.CreateDirectory(Path.GetDirectoryName(licensePath)!);
        File.WriteAllText(licensePath, JsonSerializer.Serialize(expected));

        LicenseStore store = CreateStore(environment, new LicenseConfigs { LicenseFilePath = "licenses/license.json" });

        LicensePayload? payload = store.Load();

        payload.Should().NotBeNull();
        payload!.LicenseId.Should().Be("LIC-001");
        payload.TenantId.Should().Be("tenant-a");
    }

    [Fact]
    public void Load_Falls_Back_To_Activation_Proof_When_Primary_File_Is_Raw_Key_Format()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        string licensePath = Path.Combine(temp.Path, "licenses", "license.json");
        string proofPath = Path.Combine(temp.Path, "licenses", "activation-proof.json");
        Directory.CreateDirectory(Path.GetDirectoryName(licensePath)!);
        File.WriteAllText(licensePath, """{"LicenseKey":"MRR-RAW"}""");
        File.WriteAllText(proofPath, """
            {
              "proofId": "proof-1",
              "licenseId": "LIC-PROOF",
              "signedLicensePayload": {
                "licenseId": "LIC-PROOF",
                "tenantId": "tenant-proof"
              }
            }
            """);

        LicenseStore store = CreateStore(
            environment,
            new LicenseConfigs
            {
                LicenseFilePath = "licenses/license.json",
                ActivationProofPath = "licenses/activation-proof.json"
            });

        LicensePayload? payload = store.Load();

        payload.Should().NotBeNull();
        payload!.LicenseId.Should().Be("LIC-PROOF");
        payload.TenantId.Should().Be("tenant-proof");
    }

    [Fact]
    public void Save_And_LoadActivationProof_SaveActivationJwt_Create_Files_In_Relative_Paths()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        LicenseConfigs configs = new()
        {
            LicenseFilePath = "licenses/license.json",
            ActivationProofPath = "licenses/activation-proof.json",
            ActivationJwtPath = "licenses/activation-jwt.txt"
        };
        LicenseStore store = CreateStore(environment, configs);
        LicensePayload payload = new() { LicenseId = "LIC-SAVE", AllowedFeatures = ["f1"] };
        ActivationProof proof = new()
        {
            ProofId = "proof-save",
            LicenseId = "LIC-SAVE",
            SignedLicensePayload = new LicensePayload { LicenseId = "LIC-SAVE" }
        };

        store.Save(payload);
        store.SaveActivationProof(proof);
        store.SaveActivationJwt(" jwt-token ");

        File.Exists(Path.Combine(temp.Path, "licenses", "license.json")).Should().BeTrue();
        File.Exists(Path.Combine(temp.Path, "licenses", "activation-proof.json")).Should().BeTrue();
        File.Exists(Path.Combine(temp.Path, "licenses", "activation-jwt.txt")).Should().BeTrue();
        store.LoadActivationProof()!.LicenseId.Should().Be("LIC-SAVE");
        store.LoadActivationJwt().Should().Be("jwt-token");
    }

    [Fact]
    public void LoadPublicKeyPem_Prefers_Dev_Key_And_Falls_Back_To_Standard_Key()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        LicenseStore store = CreateStore(
            environment,
            new LicenseConfigs
            {
                ActivationJwtPath = "licenses/activation-jwt.txt"
            });
        string licenseDir = Path.Combine(temp.Path, "licenses");
        Directory.CreateDirectory(licenseDir);
        File.WriteAllText(Path.Combine(licenseDir, "activation-jwt.txt"), "jwt");
        File.WriteAllText(Path.Combine(licenseDir, "public_key.pem"), "standard-key");

        store.LoadPublicKeyPem().Should().Be("standard-key");

        File.WriteAllText(Path.Combine(licenseDir, "dev_license_public.pem"), "dev-key");

        store.LoadPublicKeyPem().Should().Be("dev-key");
    }

    [Fact]
    public void LoadActivationProof_Returns_Null_When_File_Is_Invalid_Json()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        string proofPath = Path.Combine(temp.Path, "licenses", "activation-proof.json");
        Directory.CreateDirectory(Path.GetDirectoryName(proofPath)!);
        File.WriteAllText(proofPath, "{ invalid json");

        LicenseStore store = CreateStore(environment, new LicenseConfigs { ActivationProofPath = "licenses/activation-proof.json" });

        store.LoadActivationProof().Should().BeNull();
    }

    private static LicenseStore CreateStore(IHostEnvironment environment, LicenseConfigs configs)
    {
        return new LicenseStore(environment, configs, new MJsonSerializeService());
    }

    private static IHostEnvironment CreateEnvironment(string contentRootPath)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(contentRootPath);
        return environment;
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
