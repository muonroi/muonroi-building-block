using Muonroi.Governance.Compliance;
using Muonroi.Governance.ControlPlane;

namespace Muonroi.BuildingBlock.Test;

public class MComplianceExportServiceTests
{
    [Fact]
    public async Task ExportAsync_ExportsChainAndControlPlane_WithValidHashContinuity()
    {
        using ComplianceHarness harness = new();
        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1"
        };
        harness.ChainStore.Append(entry);
        FingerprintChainEntry chainEntry = new()
        {
            Sequence = 2,
            TenantId = "tenant-a",
            ActionType = "api.update",
            Signature = "sig-2"
        };
        harness.ChainStore.Append(chainEntry);
        MControlPlaneAuditRecord item = new()
        {
            AuditId = "audit-001",
            EventType = "license.issued",
            EntityType = "license",
            EntityId = "lic-001",
            Actor = "ops",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            DataHash = "hash-1",
            SignatureAlgorithm = "RSA-SHA256",
            SignatureKeyId = "key-1",
            Signature = "sig-audit"
        };
        harness.ControlPlaneStore.Registry.AuditTrail.Add(item);

        MComplianceExportRunResult result = await harness.ExportService.ExportAsync();

        Assert.True(result.IsEnabled);
        Assert.Equal(3, result.ExportedCount);
        Assert.Equal(2, result.ChainEntryCount);
        Assert.Equal(1, result.ControlPlaneAuditCount);

        IReadOnlyList<MComplianceExportRecord> records = await harness.ExportService.GetExportRecordsAsync(new MComplianceExportQuery());
        Assert.Equal(3, records.Count);
        Assert.All(records, record => Assert.False(string.IsNullOrWhiteSpace(record.RecordHash)));
        Assert.Equal("GENESIS", records[0].PreviousHash);
        Assert.Equal(records[0].RecordHash, records[1].PreviousHash);
        Assert.Equal(records[1].RecordHash, records[2].PreviousHash);

        MComplianceVerificationResult verify = await harness.ExportService.VerifyAsync();
        Assert.True(verify.IsValid);
        Assert.Equal(3, verify.CheckedCount);
    }

    [Fact]
    public async Task ExportAsync_SecondRun_OnlyExportsIncrementalEvents()
    {
        using ComplianceHarness harness = new();
        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1"
        };
        harness.ChainStore.Append(entry);
        MControlPlaneAuditRecord item = new()
        {
            AuditId = "audit-001",
            EventType = "license.issued",
            EntityType = "license",
            EntityId = "lic-001",
            Actor = "ops",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            DataHash = "hash-1",
            SignatureAlgorithm = "RSA-SHA256",
            SignatureKeyId = "key-1",
            Signature = "sig-audit"
        };
        harness.ControlPlaneStore.Registry.AuditTrail.Add(item);

        MComplianceExportRunResult first = await harness.ExportService.ExportAsync();
        Assert.Equal(2, first.ExportedCount);

        FingerprintChainEntry chainEntry = new()
        {
            Sequence = 2,
            TenantId = "tenant-a",
            ActionType = "api.create",
            Signature = "sig-2"
        };
        harness.ChainStore.Append(chainEntry);
        MControlPlaneAuditRecord record = new()
        {
            AuditId = "audit-002",
            EventType = "policy.activated",
            EntityType = "policy-bundle",
            EntityId = "bundle-002",
            Actor = "ops",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            DataHash = "hash-2",
            SignatureAlgorithm = "RSA-SHA256",
            SignatureKeyId = "key-1",
            Signature = "sig-audit-2"
        };
        harness.ControlPlaneStore.Registry.AuditTrail.Add(record);

        MComplianceExportRunResult second = await harness.ExportService.ExportAsync();
        Assert.Equal(2, second.ExportedCount);

        IReadOnlyList<MComplianceExportRecord> records = await harness.ExportService.GetExportRecordsAsync(new MComplianceExportQuery());
        Assert.Equal(4, records.Count);
        Assert.Equal([1, 2, 3, 4], records.Select(x => x.ExportSequence).ToArray());
    }

    [Fact]
    public async Task VerifyAsync_DetectsTamperedRecordHash()
    {
        using ComplianceHarness harness = new();
        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1"
        };
        harness.ChainStore.Append(entry);

        MComplianceExportRunResult result = await harness.ExportService.ExportAsync();
        Assert.Equal(1, result.ExportedCount);
        Assert.NotNull(result.ExportFilePath);
        Assert.True(File.Exists(result.ExportFilePath));

        IReadOnlyList<MComplianceExportRecord> records = await harness.ExportService.GetExportRecordsAsync(new MComplianceExportQuery());
        string[] lines = File.ReadAllLines(result.ExportFilePath!);
        lines[0] = lines[0].Replace(records[0].RecordHash, "DEADBEEF", StringComparison.OrdinalIgnoreCase);
        File.WriteAllLines(result.ExportFilePath!, lines);

        MComplianceVerificationResult verify = await harness.ExportService.VerifyAsync();
        Assert.False(verify.IsValid);
        Assert.Equal(1, verify.FirstInvalidSequence);
    }

    private sealed class ComplianceHarness : IDisposable
    {
        private readonly string _root;

        public InMemoryChainStore ChainStore { get; } = new();
        public InMemoryControlPlaneStore ControlPlaneStore { get; } = new();
        public MComplianceExportService ExportService { get; }

        public ComplianceHarness()
        {
            _root = Path.Combine(Path.GetTempPath(), "Muonroi.Governance.Compliance-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            LicenseConfigs config = new()
            {
                ProjectSeed = "1234567890ABCDEF",
                Compliance = new MComplianceConfigs
                {
                    Enabled = true,
                    ExportRootPath = Path.Combine(_root, "compliance"),
                    ExportFileName = "export.ndjson",
                    CheckpointFileName = "checkpoint.json",
                    EvidencePackFolderName = "packs",
                    EnableAutoPruneEvidencePacks = true,
                    EvidencePackRetentionDays = 7
                }
            };

            Mock<IHostEnvironment> env = new();
            env.SetupGet(x => x.ContentRootPath).Returns(_root);

            ExportService = new MComplianceExportService(
                config,
                ChainStore,
                [ControlPlaneStore],
                env.Object,
                NullLogger<MComplianceExportService>.Instance);
        }

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

        public MControlPlaneRegistry Load()
        {
            return Registry;
        }

        public void Save(MControlPlaneRegistry registry)
        {
            Registry.Licenses = registry.Licenses;
            Registry.PolicyBundles = registry.PolicyBundles;
            Registry.AuditTrail = registry.AuditTrail;
            Registry.UpdatedAtUtc = registry.UpdatedAtUtc;
            Registry.CreatedAtUtc = registry.CreatedAtUtc;
            Registry.SchemaVersion = registry.SchemaVersion;
        }
    }

    private sealed class InMemoryChainStore : IFingerprintChainStore
    {
        private readonly List<FingerprintChainEntry> _entries = [];

        public void Append(FingerprintChainEntry entry)
        {
            _entries.Add(entry);
        }

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

        public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null,
            string? tenantId = null)
        {
            string partition = AuditTrailTenantPartition.Normalize(tenantId);
            IEnumerable<FingerprintChainEntry> query = _entries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                query = query.Where(x => AuditTrailTenantPartition.Normalize(x.TenantId) == partition);
            }

            if (afterSequence.HasValue)
            {
                query = query.Where(x => x.Sequence > afterSequence.Value);
            }

            return [.. query
                .OrderBy(x => x.Sequence)
                .Take(count)];
        }

        public IEnumerable<string> GetTenantPartitions()
        {
            return [.. _entries
                .Select(x => AuditTrailTenantPartition.Normalize(x.TenantId))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }
    }
}
