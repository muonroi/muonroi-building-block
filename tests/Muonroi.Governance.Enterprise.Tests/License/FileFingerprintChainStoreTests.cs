using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
using NSubstitute;
using Xunit;

namespace Muonroi.Governance.Enterprise.Tests.License;

public sealed class FileFingerprintChainStoreTests : IDisposable
{
    private readonly string _root;
    private readonly IHostEnvironment _environment;

    public FileFingerprintChainStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "muonroi-fingerprint-chain-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _environment = Substitute.For<IHostEnvironment>();
        _environment.ContentRootPath.Returns(_root);
    }

    [Fact]
    public void GetLastSignature_And_Sequence_Default_To_Genesis_When_File_Does_Not_Exist()
    {
        FileFingerprintChainStore store = CreateStore("chains/audit.ndjson");

        store.GetLastSignature().Should().Be("GENESIS");
        store.GetLastSequence().Should().Be(0);
        store.GetTenantPartitions().Should().Contain(AuditTrailTenantPartition.HostPartition);
    }

    [Fact]
    public void Append_Persists_Entries_And_Normalizes_TenantPartitions()
    {
        FileFingerprintChainStore store = CreateStore("chains/audit.ndjson");
        store.Append(new FingerprintChainEntry
        {
            Sequence = 2,
            TenantId = "tenant-a",
            ActionType = "api.update",
            Signature = "sig-2"
        });

        string filePath = Path.Combine(_root, "chains", "audit.ndjson");
        string content = File.ReadAllText(filePath);

        store.GetLastSignature("tenant-a").Should().Be("sig-2");
        store.GetLastSequence("tenant-a").Should().Be(2);
        store.GetTenantPartitions().Should().Contain("tenant-a");
        content.Should().Contain("\"TenantId\":\"tenant-a\"");
    }

    [Fact]
    public void GetRecentEntries_Filters_By_AfterSequence_And_Tenant()
    {
        string filePath = Path.Combine(_root, "chains", "audit.ndjson");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllLines(filePath,
        [
            """{"Sequence":1,"TenantId":"tenant-a","Signature":"sig-1"}""",
            """{"Sequence":2,"TenantId":"tenant-a","Signature":"sig-2"}""",
            """{"Sequence":3,"TenantId":"tenant-b","Signature":"sig-3"}"""
        ]);

        FileFingerprintChainStore store = CreateStore("chains/audit.ndjson");

        List<FingerprintChainEntry> tenantA = [.. store.GetRecentEntries(10, 1, "tenant-a")];
        List<FingerprintChainEntry> all = [.. store.GetRecentEntries(2)];

        tenantA.Should().HaveCount(1);
        tenantA[0].Sequence.Should().Be(2);
        all.Should().HaveCount(2);
        all.Select(x => x.Sequence).Should().ContainInOrder(2L, 3L);
    }

    [Fact]
    public void EnsureLoaded_Uses_Highest_Sequence_Per_Tenant_From_Existing_File()
    {
        string filePath = Path.Combine(_root, "chains", "audit.ndjson");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllLines(filePath,
        [
            """{"Sequence":1,"TenantId":"tenant-a","Signature":"sig-1"}""",
            """{"Sequence":5,"TenantId":"tenant-a","Signature":"sig-5"}""",
            """{"Sequence":3,"TenantId":"tenant-b","Signature":"sig-3"}"""
        ]);

        FileFingerprintChainStore store = CreateStore("chains/audit.ndjson");

        store.GetLastSequence("tenant-a").Should().Be(5);
        store.GetLastSignature("tenant-a").Should().Be("sig-5");
        store.GetLastSequence("tenant-b").Should().Be(3);
    }

    [Fact]
    public void Append_With_Empty_Path_Does_Nothing()
    {
        LicenseConfigs configs = new() { ChainFilePath = null };
        FileFingerprintChainStore store = new(_environment, configs, new MJsonSerializeService());

        Action act = () => store.Append(new FingerprintChainEntry { Sequence = 1, Signature = "sig" });

        act.Should().NotThrow();
    }

    private FileFingerprintChainStore CreateStore(string relativePath)
    {
        return new FileFingerprintChainStore(
            _environment,
            new LicenseConfigs
            {
                ChainFilePath = relativePath,
                ChainStorage = LicenseChainStorage.File
            },
            new MJsonSerializeService());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
