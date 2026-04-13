using Muonroi.Governance.Compliance;
using Muonroi.Governance.ControlPlane;

namespace Muonroi.BuildingBlock.Test;

public class MComplianceEvidencePackServiceTests
{
    [Fact]
    public async Task GenerateAsync_CreatesEvidencePackWithSummaryAndSignature()
    {
        using EvidenceHarness harness = new();
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
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackRequest request = new()
        {
            TenantId = "tenant-a",
            IncludeRecords = true
        };
        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(request);

        Assert.True(File.Exists(result.OutputPath));
        Assert.NotNull(result.Pack);
        Assert.Equal(2, result.Pack.Summary.TotalRecords);
        Assert.True(result.Pack.Verification.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Pack.PackHash));
        Assert.False(string.IsNullOrWhiteSpace(result.Pack.Signature));
        Assert.Equal(2, result.Pack.Records?.Count);
    }

    [Fact]
    public async Task PruneEvidencePacksAsync_DeletesExpiredPackFiles()
    {
        using EvidenceHarness harness = new();
        string packFolder = Path.Combine(harness.ExportRoot, "packs");
        Directory.CreateDirectory(packFolder);
        string oldPack = Path.Combine(packFolder, "evidence-pack-old.json");
        await File.WriteAllTextAsync(oldPack, "{}");
        File.SetLastWriteTimeUtc(oldPack, DateTime.UtcNow.AddDays(-10));

        int deleted = await harness.ExportService.PruneEvidencePacksAsync();

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldPack));
    }

    private sealed class EvidenceHarness : IDisposable
    {
        private readonly string _root;

        public string ExportRoot { get; }
        public MComplianceExportService ExportService { get; }
        public MComplianceEvidencePackService PackService { get; }
        public InMemoryChainStore ChainStore { get; } = new();

        public EvidenceHarness()
        {
            _root = Path.Combine(Path.GetTempPath(), "Muonroi.Governance.Compliance-pack-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            ExportRoot = Path.Combine(_root, "compliance");

            LicenseConfigs config = new()
            {
                ProjectSeed = "1234567890ABCDEF",
                Compliance = new MComplianceConfigs
                {
                    Enabled = true,
                    ExportRootPath = ExportRoot,
                    ExportFileName = "export.ndjson",
                    CheckpointFileName = "checkpoint.json",
                    EvidencePackFolderName = "packs",
                    EvidencePackRetentionDays = 1,
                    EnableAutoPruneEvidencePacks = true
                }
            };

            Mock<IHostEnvironment> env = new();
            env.SetupGet(x => x.ContentRootPath).Returns(_root);

            ExportService = new MComplianceExportService(
                config,
                ChainStore,
                [],
                env.Object,
                NullLogger<MComplianceExportService>.Instance);

            PackService = new MComplianceEvidencePackService(config, ExportService, env.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
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

            return query
                .OrderBy(x => x.Sequence)
                .Take(count)
                .ToList();
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
