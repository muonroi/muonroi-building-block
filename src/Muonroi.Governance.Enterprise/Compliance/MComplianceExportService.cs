using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Governance.ControlPlane;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.Compliance;

/// <summary>
/// Represents the MCompliance Export Service.
/// </summary>
public sealed class MComplianceExportService(
    LicenseConfigs licenseConfigs,
    IFingerprintChainStore chainStore,
    IEnumerable<IMControlPlaneStore> controlPlaneStores,
    IHostEnvironment? hostEnvironment = null,
    IMLog<MComplianceExportService>? logger = null) : IMComplianceExportService
{
    private const string GenesisHash = "GENESIS";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LicenseConfigs _licenseConfigs = MGuard.NotNull(licenseConfigs);
    private readonly IFingerprintChainStore _chainStore = MGuard.NotNull(chainStore);
    private readonly IEnumerable<IMControlPlaneStore> _controlPlaneStores =
        controlPlaneStores ?? [];
    private readonly IHostEnvironment? _hostEnvironment = hostEnvironment;
    private readonly IMLog<MComplianceExportService>? _logger = logger;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the Is Enabled.
    /// </summary>
    public bool IsEnabled => _licenseConfigs.Compliance.Enabled;

    /// <summary>
    /// Executes the Export Async operation.
    /// </summary>
    public async Task<MComplianceExportRunResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        MCompliancePaths paths = ResolvePaths();
        if (!IsEnabled)
        {
            return new MComplianceExportRunResult
            {
                IsEnabled = false,
                ExportFilePath = paths.ExportFilePath,
                CheckpointFilePath = paths.CheckpointFilePath,
                LastRecordHash = GenesisHash
            };
        }

        lock (_lock)
        {
            Directory.CreateDirectory(paths.RootPath);
            Directory.CreateDirectory(paths.EvidencePackPath);
        }

        MComplianceExportState state = LoadState(paths.CheckpointFilePath);
        List<MComplianceExportRecord> exported = [];
        long currentSequence = state.LastExportSequence;
        string currentHash = string.IsNullOrWhiteSpace(state.LastRecordHash) ? GenesisHash : state.LastRecordHash;
        int chainCount = 0;
        int controlPlaneCount = 0;

        IEnumerable<string> tenants = ResolveTenants(state);
        foreach (string tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long afterSequence = state.LastChainSequenceByTenant.TryGetValue(tenant, out long knownSequence)
                ? knownSequence
                : 0;

            List<FingerprintChainEntry> entries = [.. _chainStore
                .GetRecentEntries(int.MaxValue, afterSequence, tenant)
                .Where(x => x.Sequence > afterSequence)
                .OrderBy(x => x.Sequence)];
            if (entries.Count == 0)
            {
                continue;
            }

            foreach (FingerprintChainEntry? entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentSequence += 1;

                string payloadJson = JsonSerializer.Serialize(new // MBB002-exempt: requires custom JsonOptions (camelCase + WhenWritingNull) not available in wrapper
                {
                    entry.Sequence,
                    entry.Timestamp,
                    TenantId = AuditTrailTenantPartition.Normalize(entry.TenantId),
                    entry.ActionType,
                    entry.ActionName,
                    entry.PayloadHash,
                    entry.PreviousSignature,
                    entry.Signature
                }, JsonOptions);

                MComplianceExportRecord record = BuildRecord(
                    sequence: currentSequence,
                    occurredAtUtc: entry.Timestamp,
                    source: MComplianceExportSource.AuditTrailChain,
                    eventType: string.IsNullOrWhiteSpace(entry.ActionType) ? "audit-trail.action" : entry.ActionType.Trim(),
                    tenantId: AuditTrailTenantPartition.Normalize(entry.TenantId),
                    entityType: "chain-entry",
                    entityId: $"{AuditTrailTenantPartition.Normalize(entry.TenantId)}:{entry.Sequence}",
                    payloadHash: ComputeSha256Hex(payloadJson),
                    previousHash: currentHash);

                exported.Add(record);
                currentHash = record.RecordHash;
                state.LastChainSequenceByTenant[tenant] = entry.Sequence;
                chainCount += 1;
            }
        }

        IMControlPlaneStore? controlPlaneStore = _controlPlaneStores.FirstOrDefault();
        if (controlPlaneStore != null)
        {
            MControlPlaneRegistry registry;
            try
            {
                registry = controlPlaneStore.Load();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to load control-plane registry for compliance export.");
                registry = new MControlPlaneRegistry();
            }

            List<MControlPlaneAuditRecord> sortedAudit = [.. registry.AuditTrail
                .OrderBy(x => x.OccurredAt)
                .ThenBy(x => x.AuditId, StringComparer.Ordinal)];

            foreach (MControlPlaneAuditRecord? audit in sortedAudit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string cursor = BuildControlPlaneCursor(audit);
                if (!IsCursorAfter(cursor, state.LastControlPlaneAuditCursor))
                {
                    continue;
                }

                currentSequence += 1;

                string payloadJson = JsonSerializer.Serialize(new // MBB002-exempt: requires custom JsonOptions (camelCase + WhenWritingNull) not available in wrapper
                {
                    audit.EventType,
                    audit.EntityType,
                    audit.EntityId,
                    audit.Actor,
                    audit.OccurredAt,
                    audit.DataHash,
                    audit.SignatureAlgorithm,
                    audit.SignatureKeyId,
                    audit.Signature
                }, JsonOptions);

                MComplianceExportRecord record = BuildRecord(
                    sequence: currentSequence,
                    occurredAtUtc: audit.OccurredAt,
                    source: MComplianceExportSource.ControlPlaneAudit,
                    eventType: string.IsNullOrWhiteSpace(audit.EventType) ? "control-plane.audit" : audit.EventType.Trim(),
                    tenantId: null,
                    entityType: string.IsNullOrWhiteSpace(audit.EntityType) ? "unknown" : audit.EntityType.Trim(),
                    entityId: string.IsNullOrWhiteSpace(audit.EntityId) ? audit.AuditId : audit.EntityId.Trim(),
                    payloadHash: ComputeSha256Hex(payloadJson),
                    previousHash: currentHash);

                exported.Add(record);
                currentHash = record.RecordHash;
                state.LastControlPlaneAuditCursor = cursor;
                controlPlaneCount += 1;
            }
        }

        if (exported.Count > 0)
        {
            await AppendRecordsAsync(paths.ExportFilePath, exported, cancellationToken);
        }

        state.LastExportSequence = currentSequence;
        state.LastRecordHash = currentHash;
        state.LastExportedAtUtc = DateTimeOffset.UtcNow;
        SaveState(paths.CheckpointFilePath, state);

        return new MComplianceExportRunResult
        {
            IsEnabled = true,
            ExportedCount = exported.Count,
            ChainEntryCount = chainCount,
            ControlPlaneAuditCount = controlPlaneCount,
            ExportFilePath = paths.ExportFilePath,
            CheckpointFilePath = paths.CheckpointFilePath,
            LastRecordHash = state.LastRecordHash
        };
    }

    /// <summary>
    /// Executes the Get Export Records Async operation.
    /// </summary>
    public async Task<IReadOnlyList<MComplianceExportRecord>> GetExportRecordsAsync(
        MComplianceExportQuery query,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(query);
        MCompliancePaths paths = ResolvePaths();
        if (!File.Exists(paths.ExportFilePath))
        {
            return [];
        }

        List<MComplianceExportRecord> records = [];
        using FileStream stream = new(paths.ExportFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            MComplianceExportRecord? record;
            try
            {
                record = JsonSerializer.Deserialize<MComplianceExportRecord>(line, JsonOptions); // MBB002-exempt: requires custom JsonOptions (camelCase + WhenWritingNull) not available in wrapper
            }
            catch
            {
                continue;
            }

            if (record == null)
            {
                continue;
            }

            if (query.StartUtc.HasValue && record.OccurredAtUtc < query.StartUtc.Value)
            {
                continue;
            }

            if (query.EndUtc.HasValue && record.OccurredAtUtc > query.EndUtc.Value)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query.TenantId) &&
                !string.Equals(record.TenantId, query.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (query.Source.HasValue && record.Source != query.Source.Value)
            {
                continue;
            }

            records.Add(record);
        }

        records = [.. records.OrderBy(x => x.ExportSequence)];

        int maxRecords = query.MaxRecords.GetValueOrDefault();
        if (maxRecords > 0 && records.Count > maxRecords)
        {
            records = [.. records.TakeLast(maxRecords)];
        }

        return records;
    }

    /// <summary>
    /// Executes the Verify Async operation.
    /// </summary>
    public async Task<MComplianceVerificationResult> VerifyAsync(
        MComplianceVerificationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new MComplianceVerificationRequest();
        MComplianceExportQuery query = new()
        {
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            TenantId = request.TenantId,
            Source = request.Source
        };

        IReadOnlyList<MComplianceExportRecord> records = await GetExportRecordsAsync(query, cancellationToken);
        if (records.Count == 0)
        {
            return new MComplianceVerificationResult
            {
                IsValid = true,
                CheckedCount = 0,
                LastComputedHash = GenesisHash
            };
        }

        long? previousSequence = null;
        string? previousRecordHash = null;

        foreach (MComplianceExportRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string expectedHash = ComputeRecordHash(record);
            if (!string.Equals(record.RecordHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new MComplianceVerificationResult
                {
                    IsValid = false,
                    CheckedCount = records.Count,
                    FirstInvalidSequence = record.ExportSequence,
                    Error = "Record hash mismatch.",
                    LastComputedHash = previousRecordHash ?? GenesisHash
                };
            }

            if (previousSequence.HasValue && record.ExportSequence <= previousSequence.Value)
            {
                return new MComplianceVerificationResult
                {
                    IsValid = false,
                    CheckedCount = records.Count,
                    FirstInvalidSequence = record.ExportSequence,
                    Error = "Export sequence ordering is invalid.",
                    LastComputedHash = previousRecordHash ?? GenesisHash
                };
            }

            if (previousRecordHash != null &&
                !string.Equals(record.PreviousHash, previousRecordHash, StringComparison.OrdinalIgnoreCase))
            {
                return new MComplianceVerificationResult
                {
                    IsValid = false,
                    CheckedCount = records.Count,
                    FirstInvalidSequence = record.ExportSequence,
                    Error = "Previous hash continuity mismatch.",
                    LastComputedHash = previousRecordHash
                };
            }

            previousSequence = record.ExportSequence;
            previousRecordHash = record.RecordHash;
        }

        return new MComplianceVerificationResult
        {
            IsValid = true,
            CheckedCount = records.Count,
            LastComputedHash = previousRecordHash ?? GenesisHash
        };
    }

    /// <summary>
    /// Executes the Prune Evidence Packs Async operation.
    /// </summary>
    public Task<int> PruneEvidencePacksAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || !_licenseConfigs.Compliance.EnableAutoPruneEvidencePacks)
        {
            return Task.FromResult(0);
        }

        int retentionDays = _licenseConfigs.Compliance.EvidencePackRetentionDays;
        if (retentionDays <= 0)
        {
            retentionDays = 1;
        }

        DateTimeOffset threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        MCompliancePaths paths = ResolvePaths();
        if (!Directory.Exists(paths.EvidencePackPath))
        {
            return Task.FromResult(0);
        }

        int deletedCount = 0;
        foreach (string file in Directory.GetFiles(paths.EvidencePackPath, "evidence-pack-*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime lastWrite = File.GetLastWriteTimeUtc(file);
            if (lastWrite >= threshold.UtcDateTime)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deletedCount += 1;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to prune compliance evidence pack '{File}'.", file);
            }
        }

        return Task.FromResult(deletedCount);
    }

    private IEnumerable<string> ResolveTenants(MComplianceExportState state)
    {
        HashSet<string> tenants = new(StringComparer.OrdinalIgnoreCase);
        foreach (string tenant in _chainStore.GetTenantPartitions())
        {
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                tenants.Add(AuditTrailTenantPartition.Normalize(tenant));
            }
        }

        foreach (string tenant in state.LastChainSequenceByTenant.Keys)
        {
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                tenants.Add(AuditTrailTenantPartition.Normalize(tenant));
            }
        }

        if (tenants.Count == 0)
        {
            tenants.Add(AuditTrailTenantPartition.HostPartition);
        }

        return tenants.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static MComplianceExportRecord BuildRecord(
        long sequence,
        DateTimeOffset occurredAtUtc,
        MComplianceExportSource source,
        string eventType,
        string? tenantId,
        string entityType,
        string entityId,
        string payloadHash,
        string previousHash)
    {
        MComplianceExportRecord record = new()
        {
            ExportSequence = sequence,
            OccurredAtUtc = occurredAtUtc,
            Source = source,
            EventType = eventType,
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
            EntityType = entityType,
            EntityId = entityId,
            PayloadHash = payloadHash,
            PreviousHash = string.IsNullOrWhiteSpace(previousHash) ? GenesisHash : previousHash
        };
        record.RecordHash = ComputeRecordHash(record);
        return record;
    }

    private static string ComputeRecordHash(MComplianceExportRecord record)
    {
        string material =
            $"{record.ExportSequence}|{record.OccurredAtUtc:O}|{record.Source}|{record.EventType}|{record.TenantId ?? string.Empty}|{record.EntityType}|{record.EntityId}|{record.PayloadHash}|{record.PreviousHash}";
        return ComputeSha256Hex(material);
    }

    private static string ComputeSha256Hex(string raw)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(raw ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string BuildControlPlaneCursor(MControlPlaneAuditRecord record)
    {
        return $"{record.OccurredAt:O}|{record.AuditId}";
    }

    private static bool IsCursorAfter(string cursor, string? lastCursor)
    {
        if (string.IsNullOrWhiteSpace(lastCursor))
        {
            return true;
        }

        return string.CompareOrdinal(cursor, lastCursor) > 0;
    }

    private MComplianceExportState LoadState(string checkpointPath)
    {
        lock (_lock)
        {
            if (!File.Exists(checkpointPath))
            {
                return new MComplianceExportState();
            }

            try
            {
                string json = File.ReadAllText(checkpointPath);
                MComplianceExportState state = JsonSerializer.Deserialize<MComplianceExportState>(json, JsonOptions) ?? new MComplianceExportState(); // MBB002-exempt: requires custom JsonOptions not available in wrapper
                state.LastRecordHash = string.IsNullOrWhiteSpace(state.LastRecordHash) ? GenesisHash : state.LastRecordHash;
                state.LastChainSequenceByTenant ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                return state;
            }
            catch
            {
                return new MComplianceExportState();
            }
        }
    }

    private void SaveState(string checkpointPath, MComplianceExportState state)
    {
        lock (_lock)
        {
            string? folder = Path.GetDirectoryName(checkpointPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonOptions) // MBB002-exempt: requires custom JsonOptions not available in wrapper
            {
                WriteIndented = true
            });
            File.WriteAllText(checkpointPath, json);
        }
    }

    private static async Task AppendRecordsAsync(
        string exportFilePath,
        IReadOnlyCollection<MComplianceExportRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return;
        }

        string? folder = Path.GetDirectoryName(exportFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await using FileStream fileStream = new(exportFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using StreamWriter writer = new(fileStream);
        foreach (MComplianceExportRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json = JsonSerializer.Serialize(record, JsonOptions); // MBB002-exempt: requires custom JsonOptions not available in wrapper
            await writer.WriteLineAsync(json);
        }
    }

    private MCompliancePaths ResolvePaths()
    {
        MComplianceConfigs compliance = _licenseConfigs.Compliance;
        string root = compliance.ExportRootPath;
        if (!Path.IsPathRooted(root))
        {
            string basePath = !string.IsNullOrWhiteSpace(_hostEnvironment?.ContentRootPath)
                ? _hostEnvironment.ContentRootPath
                : AppContext.BaseDirectory;
            root = Path.GetFullPath(Path.Combine(basePath, root));
        }

        string exportFileName = string.IsNullOrWhiteSpace(compliance.ExportFileName)
            ? "compliance-export.ndjson"
            : compliance.ExportFileName.Trim();
        string checkpointFileName = string.IsNullOrWhiteSpace(compliance.CheckpointFileName)
            ? "compliance-export.checkpoint.json"
            : compliance.CheckpointFileName.Trim();
        string evidenceFolderName = string.IsNullOrWhiteSpace(compliance.EvidencePackFolderName)
            ? "evidence-packs"
            : compliance.EvidencePackFolderName.Trim();

        return new MCompliancePaths(
            RootPath: root,
            ExportFilePath: Path.Combine(root, exportFileName),
            CheckpointFilePath: Path.Combine(root, checkpointFileName),
            EvidencePackPath: Path.Combine(root, evidenceFolderName));
    }

    private sealed record MCompliancePaths(
        string RootPath,
        string ExportFilePath,
        string CheckpointFilePath,
        string EvidencePackPath);
}
