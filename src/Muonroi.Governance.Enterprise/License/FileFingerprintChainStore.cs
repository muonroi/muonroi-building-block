using Muonroi.Governance.Abstractions.License;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the File Fingerprint Chain Store.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Fingerprint chain state is validated non-null by lock-guarded _loaded flag before access.")]
public sealed class FileFingerprintChainStore(IHostEnvironment? environment, LicenseConfigs configs, IMJsonSerializeService jsonSerializeService)
    : IFingerprintChainStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (long Sequence, string Signature)> _stateByTenant =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    /// <summary>
    /// Executes the Get Last Signature operation.
    /// </summary>
    public string? GetLastSignature(string? tenantId = null)
    {
        EnsureLoaded();
        string partition = AuditTrailTenantPartition.Normalize(tenantId);
        return _stateByTenant.TryGetValue(partition, out (long Sequence, string Signature) state) ? state.Signature : "GENESIS";
    }

    /// <summary>
    /// Executes the Get Last Sequence operation.
    /// </summary>
    public long GetLastSequence(string? tenantId = null)
    {
        EnsureLoaded();
        string partition = AuditTrailTenantPartition.Normalize(tenantId);
        return _stateByTenant.TryGetValue(partition, out (long Sequence, string Signature) state) ? state.Sequence : 0;
    }

    /// <summary>
    /// Executes the Append operation.
    /// </summary>
    public void Append(FingerprintChainEntry entry)
    {
        string? path = ResolvePath(configs.ChainFilePath, environment);
        if (string.IsNullOrWhiteSpace(path)) return;
        string partition = AuditTrailTenantPartition.Normalize(entry.TenantId);
        entry.TenantId = partition;
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = AuditTrailRuntimeTelemetry.ActivitySource.StartActivity(
            "audit-trail.store_append",
            ActivityKind.Internal);
        activity?.SetTag("audittrail.operation", "store_append");
        activity?.SetTag("tenant.id", partition);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string line = jsonSerializeService.Serialize(entry);

        try
        {
            lock (_lock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
                _stateByTenant[partition] = (entry.Sequence, entry.Signature ?? "GENESIS");
                _loaded = true;
            }
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation("store_append", status, partition, configs.ChainStorage.ToString(),
                sw.Elapsed, 1);
        }
    }

    /// <summary>
    /// Executes the Get Recent Entries operation.
    /// </summary>
    public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null)
    {
        string? path = ResolvePath(configs.ChainFilePath, environment);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return [];
        string partition = AuditTrailTenantPartition.Normalize(tenantId);
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = AuditTrailRuntimeTelemetry.ActivitySource.StartActivity(
            "audit-trail.store_read",
            ActivityKind.Internal);
        activity?.SetTag("audittrail.operation", "store_read");
        activity?.SetTag("tenant.id", string.IsNullOrWhiteSpace(tenantId) ? string.Empty : partition);

        try
        {
            lock (_lock)
            {
                IEnumerable<string> lines = File.ReadLines(path);
                IEnumerable<FingerprintChainEntry> entries = lines
                    .Select(line => jsonSerializeService.Deserialize<FingerprintChainEntry>(line))
                    .Where(e => e != null)
                    .Cast<FingerprintChainEntry>()
                    .Select(e =>
                    {
                        e.TenantId = AuditTrailTenantPartition.Normalize(e.TenantId);
                        return e;
                    });

                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    entries = entries.Where(e => string.Equals(e.TenantId, partition, StringComparison.OrdinalIgnoreCase));
                }

                if (afterSequence.HasValue)
                {
                    entries = entries.Where(e => e.Sequence > afterSequence.Value);
                }

                List<FingerprintChainEntry> materialized = [.. entries.TakeLast(count)];
                sw.Stop();
                AuditTrailRuntimeTelemetry.TrackOperation(
                    operation: "store_read",
                    status: status,
                    tenantId: string.IsNullOrWhiteSpace(tenantId) ? null : partition,
                    chainStorage: configs.ChainStorage.ToString(),
                    elapsed: sw.Elapsed,
                    entryCount: materialized.Count);
                return materialized;
            }
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            if (sw.IsRunning)
            {
                sw.Stop();
                AuditTrailRuntimeTelemetry.TrackOperation(
                    operation: "store_read",
                    status: status,
                    tenantId: string.IsNullOrWhiteSpace(tenantId) ? null : partition,
                    chainStorage: configs.ChainStorage.ToString(),
                    elapsed: sw.Elapsed);
            }
        }
    }

    /// <summary>
    /// Executes the Get Tenant Partitions operation.
    /// </summary>
    public IEnumerable<string> GetTenantPartitions()
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _stateByTenant.Keys.ToList();
        }
    }

    private void EnsureLoaded()
    {
        lock (_lock)
        {
            if (_loaded) return;

            string? path = ResolvePath(configs.ChainFilePath, environment);
            if (string.IsNullOrWhiteSpace(path))
            {
                _loaded = true;
                return;
            }

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                _stateByTenant[AuditTrailTenantPartition.HostPartition] = (0, "GENESIS");
                _loaded = true;
                return;
            }

            foreach (string line in File.ReadLines(path))
            {
                FingerprintChainEntry? entry = jsonSerializeService.Deserialize<FingerprintChainEntry>(line);
                if (entry is null) continue;

                string partition = AuditTrailTenantPartition.Normalize(entry.TenantId);
                if (!_stateByTenant.TryGetValue(partition, out (long Sequence, string Signature) current) || entry.Sequence >= current.Sequence)
                {
                    _stateByTenant[partition] = (entry.Sequence, entry.Signature ?? "GENESIS");
                }
            }

            if (_stateByTenant.Count == 0)
            {
                _stateByTenant[AuditTrailTenantPartition.HostPartition] = (0, "GENESIS");
            }

            _loaded = true;
        }
    }

    private static string? ResolvePath(string? path, IHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;
        string root = !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }
}
