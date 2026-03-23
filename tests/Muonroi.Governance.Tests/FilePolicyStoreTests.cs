using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Tests;

public sealed class FilePolicyStoreTests
{
    [Fact]
    public void Load_Returns_Null_When_File_Does_Not_Exist()
    {
        using TempDir temp = new();
        FilePolicyStore store = CreateStore(temp.Path);

        store.Load().Should().BeNull();
    }

    [Fact]
    public void Save_Then_Load_Uses_Relative_Path_And_RoundTrips_Policy()
    {
        using TempDir temp = new();
        FilePolicyStore store = CreateStore(temp.Path);
        LicensePolicy policy = new()
        {
            PolicyId = "POL-001",
            LicenseId = "LIC-001",
            Version = "2.0.0",
            FeatureQuotas = new Dictionary<string, FeatureQuota>
            {
                ["export"] = new() { MaxUsagePerDay = 50, MaxConcurrentUsage = 3 }
            }
        };

        store.Save(policy);
        LicensePolicy? loaded = store.Load();

        File.Exists(Path.Combine(temp.Path, "policies", "policy.json")).Should().BeTrue();
        loaded.Should().NotBeNull();
        loaded!.PolicyId.Should().Be("POL-001");
        loaded.FeatureQuotas["export"].MaxConcurrentUsage.Should().Be(3);
    }

    [Fact]
    public void Load_Returns_Null_When_Deserializer_Throws()
    {
        using TempDir temp = new();
        IHostEnvironment environment = CreateEnvironment(temp.Path);
        LicenseConfigs configs = new() { PolicyFilePath = "policies/policy.json" };
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        json.Deserialize<LicensePolicy>(Arg.Any<string>()).Returns(_ => throw new JsonException("boom"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "policies"));
        File.WriteAllText(Path.Combine(temp.Path, "policies", "policy.json"), "{}");
        FilePolicyStore store = new(configs, environment, json);

        store.Load().Should().BeNull();
    }

    private static FilePolicyStore CreateStore(string contentRootPath)
    {
        return new FilePolicyStore(
            new LicenseConfigs { PolicyFilePath = "policies/policy.json" },
            CreateEnvironment(contentRootPath),
            new MJsonSerializeService());
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
