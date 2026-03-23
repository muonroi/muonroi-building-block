using Microsoft.Extensions.Hosting;
using Moq;
using NSubstitute;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Governance.ControlPlane;
using Muonroi.Governance.Enterprise.Compliance;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.Tests.Compliance;

public class MComplianceEvidencePackServiceTests
{
    [Fact]
    public async Task GenerateAsync_CreatesEvidencePackWithSummaryAndSignature()
    {
        using EvidenceHarness harness = new();
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.list",
            Signature = "sig-1"
        });
        harness.ChainStore.Append(new FingerprintChainEntry
        {
            Sequence = 2,
            TenantId = "tenant-a",
            ActionType = "api.update",
            Signature = "sig-2"
        });

        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(new MComplianceEvidencePackRequest
        {
            TenantId = "tenant-a",
            IncludeRecords = true
        });

        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(2, result.Pack.Summary.TotalRecords);
        Assert.True(result.Pack.Verification.IsValid);
        Assert.Equal(2, result.Pack.Records?.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.Pack.PackHash));
        Assert.False(string.IsNullOrWhiteSpace(result.Pack.Signature));
    }

    [Fact]
    public async Task GenerateAsync_WhenExportDisabled_ShouldThrow()
    {
        Mock<IMComplianceExportService> exportService = new();
        exportService.SetupGet(x => x.IsEnabled).Returns(false);

        MComplianceEvidencePackService service = new(
            new LicenseConfigs(),
            exportService.Object,
            Mock.Of<IHostEnvironment>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(new MComplianceEvidencePackRequest()));
    }

    [Fact]
    public async Task GenerateAsync_WithRelativeOutputPath_ShouldWriteUnderEvidenceFolder()
    {
        using EvidenceHarness harness = new();
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(new MComplianceEvidencePackRequest
        {
            OutputPath = "tenant-a-pack.json",
            IncludeRecords = false
        });

        Assert.Contains(Path.Combine("packs", "tenant-a-pack.json"), result.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Pack.Records);
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
            _root = Path.Combine(Path.GetTempPath(), "Muonroi.Governance.Enterprise-pack-tests", Guid.NewGuid().ToString("N"));
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
                Substitute.For<IMLog<MComplianceExportService>>());

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

        public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null)
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
