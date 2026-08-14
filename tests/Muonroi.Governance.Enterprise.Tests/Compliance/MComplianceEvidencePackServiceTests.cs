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

        await Assert.ThrowsAsync<MInternalException>(() => service.GenerateAsync(new MComplianceEvidencePackRequest()));
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

    // ─── verify-on-load: fresh HMAC pack is trustworthy ───────────────────────

    [Fact]
    public async Task VerifyAsync_FreshHmacPack_IsTrustworthy()
    {
        using EvidenceHarness harness = new();
        harness.SeedTwoEntries();
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(
            new MComplianceEvidencePackRequest { TenantId = "tenant-a", IncludeRecords = true });

        Assert.Equal("HMACSHA256", result.Pack.SignatureAlgorithm);

        MComplianceEvidencePackVerifyResult verify = await harness.PackService.VerifyAsync(result.OutputPath);

        Assert.True(verify.SignatureValid);
        Assert.True(verify.ContentHashValid);
        Assert.True(verify.IsTrustworthy);
    }

    // ─── verify-on-load: RSA chain-of-custody round-trips ─────────────────────

    [Fact]
    public async Task VerifyAsync_RsaSignedPack_RoundTripsAndVerifies()
    {
        using MRsaControlPlaneSigner signer = MRsaControlPlaneSigner.CreateEphemeral("test-cp");
        using EvidenceHarness harness = new(signer);
        harness.SeedTwoEntries();
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(
            new MComplianceEvidencePackRequest { TenantId = "tenant-a", IncludeRecords = true });

        Assert.Equal("RSA-SHA256", result.Pack.SignatureAlgorithm);
        Assert.Equal("test-cp", result.Pack.SigningKeyId);

        MComplianceEvidencePackVerifyResult verify = await harness.PackService.VerifyAsync(result.OutputPath);

        Assert.True(verify.SignatureValid);
        Assert.True(verify.ContentHashValid);
    }

    // ─── tamper detection: altered content fails the content hash ─────────────

    [Fact]
    public async Task VerifyAsync_TamperedContent_DetectsContentMismatch()
    {
        using EvidenceHarness harness = new();
        harness.SeedTwoEntries();
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(
            new MComplianceEvidencePackRequest { TenantId = "tenant-a", IncludeRecords = true });

        // Alter a hashed field (RootHash) without touching PackHash/Signature.
        MutatePack(result.OutputPath, doc => doc.RootHash = "TAMPERED");

        MComplianceEvidencePackVerifyResult verify = await harness.PackService.VerifyAsync(result.OutputPath);

        Assert.True(verify.SignatureValid, "signature is over the unchanged PackHash");
        Assert.False(verify.ContentHashValid, "recomputed hash must not match after content tamper");
        Assert.False(verify.IsTrustworthy);
    }

    // ─── tamper detection: altered signature fails verification ───────────────

    [Fact]
    public async Task VerifyAsync_TamperedSignature_FailsSignature()
    {
        using EvidenceHarness harness = new();
        harness.SeedTwoEntries();
        _ = await harness.ExportService.ExportAsync();

        MComplianceEvidencePackResult result = await harness.PackService.GenerateAsync(
            new MComplianceEvidencePackRequest { TenantId = "tenant-a", IncludeRecords = true });

        MutatePack(result.OutputPath, doc => doc.Signature = FlipFirstHexChar(doc.Signature));

        MComplianceEvidencePackVerifyResult verify = await harness.PackService.VerifyAsync(result.OutputPath);

        Assert.False(verify.SignatureValid);
        Assert.False(verify.IsTrustworthy);
    }

    // ─── fail-closed: no key material and no signer must throw, not sign with a default ──

    [Fact]
    public async Task GenerateAsync_NoKeyMaterialNoSigner_FailsClosed()
    {
        using EvidenceHarness harness = new(signer: null, withKeyMaterial: false);
        harness.SeedTwoEntries();
        _ = await harness.ExportService.ExportAsync();

        await Assert.ThrowsAsync<MInternalException>(() =>
            harness.PackService.GenerateAsync(new MComplianceEvidencePackRequest { TenantId = "tenant-a" }));
    }

    private static readonly JsonSerializerOptions PackJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private static void MutatePack(string path, Action<MComplianceEvidencePackDocument> mutate)
    {
        string json = File.ReadAllText(path);
        MComplianceEvidencePackDocument doc =
            JsonSerializer.Deserialize<MComplianceEvidencePackDocument>(json, PackJsonOptions)!;
        mutate(doc);
        File.WriteAllText(path, JsonSerializer.Serialize(doc, PackJsonOptions));
    }

    private static string FlipFirstHexChar(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return "00";
        char first = hex[0] == 'A' ? 'B' : 'A';
        return first + hex[1..];
    }

    private sealed class EvidenceHarness : IDisposable
    {
        private readonly string _root;

        public string ExportRoot { get; }
        public MComplianceExportService ExportService { get; }
        public MComplianceEvidencePackService PackService { get; }
        public InMemoryChainStore ChainStore { get; } = new();

        public EvidenceHarness(IMControlPlaneSigner? signer = null, bool withKeyMaterial = true)
        {
            _root = Path.Combine(Path.GetTempPath(), "Muonroi.Governance.Enterprise-pack-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            ExportRoot = Path.Combine(_root, "compliance");

            LicenseConfigs config = new()
            {
                ProjectSeed = withKeyMaterial ? "1234567890ABCDEF" : string.Empty,
                FingerprintSalt = string.Empty,
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

            PackService = new MComplianceEvidencePackService(config, ExportService, env.Object, signer);
        }

        public void SeedTwoEntries()
        {
            ChainStore.Append(new FingerprintChainEntry
            {
                Sequence = 1,
                TenantId = "tenant-a",
                ActionType = "api.list",
                Signature = "sig-1"
            });
            ChainStore.Append(new FingerprintChainEntry
            {
                Sequence = 2,
                TenantId = "tenant-a",
                ActionType = "api.update",
                Signature = "sig-2"
            });
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
