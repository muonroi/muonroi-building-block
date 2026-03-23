using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using NSubstitute;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Governance.ControlPlane;
using Muonroi.Governance.Enterprise.Compliance;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Xunit;

namespace Muonroi.Governance.Enterprise.Tests.Compliance;

public sealed class MComplianceExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WhenDisabled_ReturnsPathsWithoutWritingFiles()
    {
        using ExportHarness harness = new(enabled: false);

        MComplianceExportRunResult result = await harness.Service.ExportAsync();

        result.IsEnabled.Should().BeFalse();
        result.ExportedCount.Should().Be(0);
        File.Exists(result.ExportFilePath!).Should().BeFalse();
        File.Exists(result.CheckpointFilePath!).Should().BeFalse();
        result.LastRecordHash.Should().Be("GENESIS");
    }

    [Fact]
    public async Task ExportAsync_Writes_Chain_And_ControlPlane_Records_And_Verifies_Them()
    {
        using ExportHarness harness = new();
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            ActionName = "list-orders",
            PayloadHash = "hash-1",
            Signature = "sig-1",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 2,
            TenantId = "tenant-a",
            ActionType = "api.update",
            ActionName = "update-order",
            PayloadHash = "hash-2",
            PreviousSignature = "sig-1",
            Signature = "sig-2",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4)
        });
        harness.ControlPlaneStore.Registry.AuditTrail.Add(new MControlPlaneAuditRecord
        {
            AuditId = "audit-1",
            EventType = "policy.approved",
            EntityType = "policy-bundle",
            EntityId = "bundle-1",
            Actor = "ops-user",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            DataHash = "cp-hash",
            SignatureAlgorithm = "RSA",
            SignatureKeyId = "kid-1",
            Signature = "signed"
        });

        MComplianceExportRunResult run = await harness.Service.ExportAsync();
        IReadOnlyList<MComplianceExportRecord> records = await harness.Service.GetExportRecordsAsync(new MComplianceExportQuery());
        MComplianceVerificationResult verification = await harness.Service.VerifyAsync();

        run.IsEnabled.Should().BeTrue();
        run.ExportedCount.Should().Be(3);
        run.ChainEntryCount.Should().Be(2);
        run.ControlPlaneAuditCount.Should().Be(1);
        records.Should().HaveCount(3);
        records.Select(x => x.ExportSequence).Should().ContainInOrder(1L, 2L, 3L);
        records[0].TenantId.Should().Be("tenant-a");
        records[2].Source.Should().Be(MComplianceExportSource.ControlPlaneAudit);
        verification.IsValid.Should().BeTrue();
        verification.CheckedCount.Should().Be(3);
        File.Exists(run.ExportFilePath!).Should().BeTrue();
        File.Exists(run.CheckpointFilePath!).Should().BeTrue();
    }

    [Fact]
    public async Task GetExportRecordsAsync_Applies_Filters_And_MaxRecords()
    {
        using ExportHarness harness = new();
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 2,
            TenantId = "tenant-b",
            ActionType = "api.delete",
            Signature = "sig-2",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-9)
        });
        await harness.Service.ExportAsync();

        IReadOnlyList<MComplianceExportRecord> filtered = await harness.Service.GetExportRecordsAsync(new MComplianceExportQuery
        {
            TenantId = "tenant-b",
            Source = MComplianceExportSource.AuditTrailChain,
            MaxRecords = 1
        });

        filtered.Should().HaveCount(1);
        filtered[0].TenantId.Should().Be("tenant-b");
        filtered[0].EventType.Should().Be("api.delete");
    }

    [Fact]
    public async Task VerifyAsync_Returns_Invalid_When_Record_Is_Tampered()
    {
        using ExportHarness harness = new();
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        MComplianceExportRunResult run = await harness.Service.ExportAsync();

        string[] lines = await File.ReadAllLinesAsync(run.ExportFilePath!);
        lines[0] = lines[0].Replace("api.list", "api.tampered", StringComparison.Ordinal);
        await File.WriteAllLinesAsync(run.ExportFilePath!, lines);

        MComplianceVerificationResult verification = await harness.Service.VerifyAsync();

        verification.IsValid.Should().BeFalse();
        verification.Error.Should().Be("Record hash mismatch.");
        verification.FirstInvalidSequence.Should().Be(1);
    }

    [Fact]
    public async Task PruneEvidencePacksAsync_Deletes_Only_Old_Packs()
    {
        using ExportHarness harness = new();
        string oldPack = Path.Combine(harness.EvidencePackPath, "evidence-pack-old.json");
        string newPack = Path.Combine(harness.EvidencePackPath, "evidence-pack-new.json");
        Directory.CreateDirectory(harness.EvidencePackPath);
        await File.WriteAllTextAsync(oldPack, "{}");
        await File.WriteAllTextAsync(newPack, "{}");
        File.SetLastWriteTimeUtc(oldPack, DateTime.UtcNow.AddDays(-5));
        File.SetLastWriteTimeUtc(newPack, DateTime.UtcNow);

        int deleted = await harness.Service.PruneEvidencePacksAsync();

        deleted.Should().Be(1);
        File.Exists(oldPack).Should().BeFalse();
        File.Exists(newPack).Should().BeTrue();
    }

    private sealed class ExportHarness : IDisposable
    {
        private readonly string _root;

        public ExportHarness(bool enabled = true)
        {
            _root = Path.Combine(Path.GetTempPath(), "Muonroi.Governance.Enterprise-export-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            ExportRoot = Path.Combine(_root, "compliance");
            EvidencePackPath = Path.Combine(ExportRoot, "packs");
            ChainStore = new InMemoryChainStore();
            ControlPlaneStore = new InMemoryControlPlaneStore();

            LicenseConfigs config = new()
            {
                Compliance = new MComplianceConfigs
                {
                    Enabled = enabled,
                    ExportRootPath = ExportRoot,
                    ExportFileName = "export.ndjson",
                    CheckpointFileName = "checkpoint.json",
                    EvidencePackFolderName = "packs",
                    EnableAutoPruneEvidencePacks = true,
                    EvidencePackRetentionDays = 1
                }
            };

            Mock<IHostEnvironment> env = new();
            env.SetupGet(x => x.ContentRootPath).Returns(_root);

            Service = new MComplianceExportService(
                config,
                ChainStore,
                [ControlPlaneStore],
                env.Object,
                Substitute.For<IMLog<MComplianceExportService>>());
        }

        public string ExportRoot { get; }
        public string EvidencePackPath { get; }
        public InMemoryChainStore ChainStore { get; }
        public InMemoryControlPlaneStore ControlPlaneStore { get; }
        public MComplianceExportService Service { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
    }

    private sealed class InMemoryControlPlaneStore : IMControlPlaneStore
    {
        public MControlPlaneRegistry Registry { get; } = new();

        public MControlPlaneRegistry Load() => Registry;

        public void Save(MControlPlaneRegistry registry)
        {
            Registry.AuditTrail = registry.AuditTrail;
            Registry.Licenses = registry.Licenses;
            Registry.PolicyBundles = registry.PolicyBundles;
            Registry.UpdatedAtUtc = registry.UpdatedAtUtc;
        }
    }

    private sealed class InMemoryChainStore : IFingerprintChainStore
    {
        private readonly List<FingerprintChainEntry> _entries = [];

        public void Append(FingerprintChainEntry entry) => _entries.Add(entry);

        public string? GetLastSignature(string? tenantId = null)
        {
            string partition = AuditTrailTenantPartition.Normalize(tenantId);
            return _entries
                .Where(x => AuditTrailTenantPartition.Normalize(x.TenantId) == partition)
                .OrderByDescending(x => x.Sequence)
                .Select(x => x.Signature)
                .FirstOrDefault();
        }

        public long GetLastSequence(string? tenantId = null)
        {
            string partition = AuditTrailTenantPartition.Normalize(tenantId);
            return _entries
                .Where(x => AuditTrailTenantPartition.Normalize(x.TenantId) == partition)
                .Select(x => x.Sequence)
                .DefaultIfEmpty(0)
                .Max();
        }

        public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null)
        {
            IEnumerable<FingerprintChainEntry> query = _entries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                string partition = AuditTrailTenantPartition.Normalize(tenantId);
                query = query.Where(x => AuditTrailTenantPartition.Normalize(x.TenantId) == partition);
            }

            if (afterSequence.HasValue)
            {
                query = query.Where(x => x.Sequence > afterSequence.Value);
            }

            return query.OrderBy(x => x.Sequence).Take(count).ToList();
        }

        public IEnumerable<string> GetTenantPartitions()
        {
            return _entries
                .Select(x => AuditTrailTenantPartition.Normalize(x.TenantId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
