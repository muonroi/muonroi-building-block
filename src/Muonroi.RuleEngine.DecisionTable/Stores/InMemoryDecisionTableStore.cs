using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.DecisionTable.Stores;

/// <summary>
/// An in-memory implementation of <see cref="IDecisionTableStore"/>.
/// </summary>
public sealed class InMemoryDecisionTableStore(IMJsonSerializeService jsonSerializeService) : IDecisionTableStore
{
    private readonly ConcurrentDictionary<string, InMemoryDecisionTableRecord> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<DecisionTableAuditEntry> _auditTrail = [];
    private readonly ConcurrentDictionary<string, List<DecisionTableVersionSnapshot>> _versionHistory = new(StringComparer.OrdinalIgnoreCase);
    private long _nextAuditId;

    /// <inheritdoc />
    public Task<DecisionTablePageResult> QueryAsync(DecisionTableQuery query, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 200);

        IEnumerable<InMemoryDecisionTableRecord> records = _tables.Values;

        if (!query.IncludeDeleted)
        {
            records = records.Where(x => !x.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            records = records.Where(x =>
                x.Table.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Table.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            records = records.Where(x => string.Equals(x.Table.TenantId, query.TenantId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.HitPolicy.HasValue)
        {
            records = records.Where(x => x.Table.HitPolicy == query.HitPolicy.Value);
        }

        InMemoryDecisionTableRecord[] ordered = [.. records.OrderByDescending(x => x.Table.ModifiedAt)];
        int total = ordered.Length;
        DecisionTableModel[] items = [.. ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => Clone(x.Table))];

        return Task.FromResult(new DecisionTablePageResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DecisionTableModel>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        DecisionTableQuery query = new()
        {
            Page = page,
            PageSize = pageSize
        };
        DecisionTablePageResult result = await QueryAsync(query, cancellationToken);

        return result.Items;
    }

    /// <inheritdoc />
    public Task<DecisionTableModel?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _tables.TryGetValue(id, out InMemoryDecisionTableRecord? record);
        if (record is null || record.IsDeleted)
        {
            return Task.FromResult<DecisionTableModel?>(null);
        }

        return Task.FromResult<DecisionTableModel?>(Clone(record.Table));
    }

    /// <inheritdoc />
    public Task SaveAsync(
        DecisionTableModel table,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _tables.TryGetValue(table.Id, out InMemoryDecisionTableRecord? existingRecord);
        DecisionTableModel? existing = existingRecord?.Table;

        DecisionTableModel snapshot = Clone(table);
        snapshot.CreatedAt = existing?.CreatedAt ?? EnsureUtcTimestamp(table.CreatedAt, now);
        snapshot.ModifiedAt = now;
        snapshot.Version = existing is null ? Math.Max(1, table.Version) : Math.Max(existing.Version + 1, table.Version + 1);
        snapshot.Rows = [.. snapshot.Rows.OrderBy(x => x.Order)];

        _tables[snapshot.Id] = new InMemoryDecisionTableRecord
        {
            Table = Clone(snapshot),
            IsDeleted = false
        };

        EnqueueVersion(snapshot, existing is null ? "create" : "update", actor, reason, now);
        EnqueueAudit(snapshot.Id, existing is null ? "create" : "update", actor, reason, Clone(snapshot), now);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DecisionTableBulkResult> BulkUpsertAsync(
        IReadOnlyList<DecisionTableModel> tables,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        List<string> ids = new(tables.Count);

        foreach (DecisionTableModel table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SaveAsync(table, actor, reason, cancellationToken);
            ids.Add(table.Id);
        }

        EnqueueAudit(null, "bulk-upsert", actor, reason, ids, DateTimeOffset.UtcNow);

        return new DecisionTableBulkResult
        {
            ProcessedCount = ids.Count,
            Ids = ids
        };
    }

    /// <inheritdoc />
    public Task<DecisionTableBulkResult> BulkDeleteAsync(
        IReadOnlyList<string> ids,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        List<string> processed = new(ids.Count);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (string? id in ids.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tables.TryGetValue(id, out InMemoryDecisionTableRecord? record) && !record.IsDeleted)
            {
                record.IsDeleted = true;
                record.Table.ModifiedAt = now;
                _tables[id] = record;
                processed.Add(id);
                EnqueueAudit(id, "delete", actor, reason, null, now);
            }
        }

        if (processed.Count > 0)
        {
            EnqueueAudit(null, "bulk-delete", actor, reason, processed, now);
        }

        return Task.FromResult(new DecisionTableBulkResult
        {
            ProcessedCount = processed.Count,
            Ids = processed
        });
    }

    /// <inheritdoc />
    public Task<bool> ReorderRowsAsync(
        string id,
        IReadOnlyList<string> orderedRowIds,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tables.TryGetValue(id, out InMemoryDecisionTableRecord? record) || record.IsDeleted)
        {
            return Task.FromResult(false);
        }

        Dictionary<string, DecisionTableRow> rowLookup = record.Table.Rows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (orderedRowIds.Count != rowLookup.Count || orderedRowIds.Any(rowId => !rowLookup.ContainsKey(rowId)))
        {
            return Task.FromResult(false);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int index = 0; index < orderedRowIds.Count; index++)
        {
            rowLookup[orderedRowIds[index]].Order = index + 1;
        }

        record.Table.Rows = [.. rowLookup.Values.OrderBy(x => x.Order)];
        record.Table.ModifiedAt = now;
        record.Table.Version += 1;
        _tables[id] = record;

        EnqueueVersion(record.Table, "reorder", actor, reason, now);
        EnqueueAudit(id, "reorder-rows", actor, reason, orderedRowIds.ToArray(), now);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DecisionTableVersionSnapshot>> GetVersionHistoryAsync(
        string id,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        if (!_versionHistory.TryGetValue(id, out List<DecisionTableVersionSnapshot>? versions))
        {
            return Task.FromResult<IReadOnlyList<DecisionTableVersionSnapshot>>([]);
        }

        DecisionTableVersionSnapshot[] result = [.. versions
            .OrderByDescending(x => x.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)];

        return Task.FromResult<IReadOnlyList<DecisionTableVersionSnapshot>>(result);
    }

    /// <inheritdoc />
    public Task<DecisionTableVersionSnapshot?> GetVersionAsync(
        string id,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (!_versionHistory.TryGetValue(id, out List<DecisionTableVersionSnapshot>? versions))
        {
            return Task.FromResult<DecisionTableVersionSnapshot?>(null);
        }

        DecisionTableVersionSnapshot? snapshot = versions.FirstOrDefault(x => x.Version == version);
        return Task.FromResult(snapshot is null ? null : Clone(snapshot));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DecisionTableAuditEntry>> GetAuditTrailAsync(
        string? id = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        DecisionTableAuditEntry[] entries = [.. _auditTrail
            .Where(x => string.IsNullOrWhiteSpace(id) || string.Equals(x.TableId, id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)];

        return Task.FromResult<IReadOnlyList<DecisionTableAuditEntry>>(entries);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return BulkDeleteAsync([id], reason: "single-delete", cancellationToken: cancellationToken);
    }

    private void EnqueueVersion(
        DecisionTableModel table,
        string changeType,
        string? actor,
        string? reason,
        DateTimeOffset timestamp)
    {
        DecisionTableVersionSnapshot snapshot = new()
        {
            TableId = table.Id,
            Version = table.Version,
            ChangeType = changeType,
            Actor = actor,
            Reason = reason,
            Timestamp = timestamp,
            Table = Clone(table)
        };

        _versionHistory.AddOrUpdate(
            table.Id,
            _ => [snapshot],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(snapshot);
                }

                return list;
            });
    }

    private void EnqueueAudit(
        string? tableId,
        string action,
        string? actor,
        string? reason,
        object? payload,
        DateTimeOffset timestamp)
    {
        string? payloadJson = payload is null ? null : jsonSerializeService.Serialize(payload);
        DecisionTableAuditEntry audit = new()
        {
            Id = Interlocked.Increment(ref _nextAuditId),
            TableId = tableId,
            Action = action,
            Actor = actor,
            Reason = reason,
            Timestamp = timestamp,
            PayloadJson = payloadJson
        };

        _auditTrail.Enqueue(audit);
    }

    private static DateTimeOffset EnsureUtcTimestamp(DateTimeOffset timestamp, DateTimeOffset fallback)
    {
        return timestamp == default ? fallback : timestamp.ToUniversalTime();
    }

    private DecisionTableModel Clone(DecisionTableModel source)
    {
        string json = jsonSerializeService.Serialize(source);
        return jsonSerializeService.Deserialize<DecisionTableModel>(json) ?? new DecisionTableModel();
    }

    private DecisionTableVersionSnapshot Clone(DecisionTableVersionSnapshot source)
    {
        return new DecisionTableVersionSnapshot
        {
            TableId = source.TableId,
            Version = source.Version,
            ChangeType = source.ChangeType,
            Actor = source.Actor,
            Reason = source.Reason,
            Timestamp = source.Timestamp,
            Table = Clone(source.Table)
        };
    }

    private sealed class InMemoryDecisionTableRecord
    {
        public required DecisionTableModel Table { get; init; }
        public bool IsDeleted { get; set; }
    }
}
