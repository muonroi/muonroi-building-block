namespace Muonroi.RuleEngine.DecisionTable.Stores;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "EF Core LINQ projections return values structurally guaranteed non-null by the query shape; MGuard.NotNull would add redundant allocations on the hot data path.")]
internal sealed class EfCoreDecisionTableStore(
    DecisionTableDbContext dbContext,
    IMJsonSerializeService jsonSerializeService,
    IMDateTimeService dateTimeService) : IDecisionTableStore
{
    public async Task<DecisionTablePageResult> QueryAsync(DecisionTableQuery query, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 200);

        IQueryable<DecisionTableRecordEntity> records = dbContext.Tables.AsNoTracking();

        if (!query.IncludeDeleted)
        {
            records = records.Where(x => !x.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            records = records.Where(x => x.Name.Contains(search) || x.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            records = records.Where(x => x.TenantId == query.TenantId);
        }

        if (query.HitPolicy.HasValue)
        {
            string hitPolicy = query.HitPolicy.Value.ToString();
            records = records.Where(x => x.HitPolicy == hitPolicy);
        }

        int total = await records.CountAsync(cancellationToken);
        List<DecisionTableRecordEntity> entities = await records
            .OrderByDescending(x => x.ModifiedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        DecisionTableModel[] items = [.. entities
            .Select(x => Deserialize(x.PayloadJson))
            .Where(x => x is not null)
            .Select(x => x!)];

        return new DecisionTablePageResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<IReadOnlyList<DecisionTableModel>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        DecisionTableQuery query = new()
        {
            Page = page,
            PageSize = pageSize
        };
        DecisionTablePageResult result = await QueryAsync(query, cancellationToken);

        return result.Items;
    }

    public async Task<DecisionTableModel?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        DecisionTableRecordEntity? entity = await dbContext.Tables
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        return entity is null ? null : Deserialize(entity.PayloadJson);
    }

    public async Task SaveAsync(
        DecisionTableModel table,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = dateTimeService.UtcNow();
        await UpsertCoreAsync(table, actor, reason, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DecisionTableBulkResult> BulkUpsertAsync(
        IReadOnlyList<DecisionTableModel> tables,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = dateTimeService.UtcNow();
        List<string> ids = new(tables.Count);

        foreach (DecisionTableModel table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpsertCoreAsync(table, actor, reason, now, cancellationToken);
            ids.Add(table.Id);
        }

        DecisionTableAuditEntity entity = new()
        {
            Action = "bulk-upsert",
            Actor = actor,
            Reason = reason,
            PayloadJson = jsonSerializeService.Serialize(ids),
            Timestamp = now
        };
        dbContext.AuditEntries.Add(entity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DecisionTableBulkResult
        {
            ProcessedCount = ids.Count,
            Ids = ids
        };
    }

    public async Task<DecisionTableBulkResult> BulkDeleteAsync(
        IReadOnlyList<string> ids,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        string[] normalizedIds = [.. ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (normalizedIds.Length == 0)
        {
            return new DecisionTableBulkResult();
        }

        DateTimeOffset now = dateTimeService.UtcNow();
        List<DecisionTableRecordEntity> entities = await dbContext.Tables
            .Where(x => normalizedIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (DecisionTableRecordEntity? entity in entities)
        {
            DecisionTableModel? snapshot = Deserialize(entity.PayloadJson);
            if (snapshot is null)
            {
                continue;
            }

            snapshot.Version = entity.Version + 1;
            snapshot.ModifiedAt = now;

            entity.Version = snapshot.Version;
            entity.ModifiedAt = now;
            entity.IsDeleted = true;
            entity.PayloadJson = Serialize(snapshot);

            AddVersionEntry(snapshot, "delete", actor, reason, now);
            AddAuditEntry(entity.Id, "delete", actor, reason, null, now);
        }

        DecisionTableAuditEntity auditEntity = new()
        {
            Action = "bulk-delete",
            Actor = actor,
            Reason = reason,
            PayloadJson = jsonSerializeService.Serialize(entities.Select(x => x.Id).ToArray()),
            Timestamp = now
        };
        dbContext.AuditEntries.Add(auditEntity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DecisionTableBulkResult
        {
            ProcessedCount = entities.Count,
            Ids = entities.Select(x => x.Id).ToArray()
        };
    }

    public async Task<bool> ReorderRowsAsync(
        string id,
        IReadOnlyList<string> orderedRowIds,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        DecisionTableRecordEntity? entity = await dbContext.Tables.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        DecisionTableModel? snapshot = Deserialize(entity.PayloadJson);
        if (snapshot is null)
        {
            return false;
        }

        Dictionary<string, DecisionTableRow> rowLookup = snapshot.Rows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (orderedRowIds.Count != rowLookup.Count || orderedRowIds.Any(rowId => !rowLookup.ContainsKey(rowId)))
        {
            return false;
        }

        for (int index = 0; index < orderedRowIds.Count; index++)
        {
            rowLookup[orderedRowIds[index]].Order = index + 1;
        }

        DateTimeOffset now = dateTimeService.UtcNow();
        snapshot.Rows = [.. rowLookup.Values.OrderBy(x => x.Order)];
        snapshot.Version = entity.Version + 1;
        snapshot.ModifiedAt = now;

        entity.Version = snapshot.Version;
        entity.ModifiedAt = now;
        entity.PayloadJson = Serialize(snapshot);
        entity.Name = snapshot.Name;
        entity.Description = snapshot.Description;
        entity.HitPolicy = snapshot.HitPolicy.ToString();
        entity.TenantId = snapshot.TenantId;

        AddVersionEntry(snapshot, "reorder", actor, reason, now);
        AddAuditEntry(entity.Id, "reorder-rows", actor, reason, jsonSerializeService.Serialize(orderedRowIds), now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DecisionTableVersionSnapshot>> GetVersionHistoryAsync(
        string id,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        List<DecisionTableVersionEntity> versions = await dbContext.Versions
            .AsNoTracking()
            .Where(x => x.TableId == id)
            .OrderByDescending(x => x.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return versions
            .Select(MapVersion)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
    }

    public async Task<DecisionTableVersionSnapshot?> GetVersionAsync(
        string id,
        int version,
        CancellationToken cancellationToken = default)
    {
        DecisionTableVersionEntity? entity = await dbContext.Versions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TableId == id && x.Version == version, cancellationToken);

        return entity is null ? null : MapVersion(entity);
    }

    public async Task<IReadOnlyList<DecisionTableAuditEntry>> GetAuditTrailAsync(
        string? id = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<DecisionTableAuditEntity> query = dbContext.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(id))
        {
            query = query.Where(x => x.TableId == id);
        }

        List<DecisionTableAuditEntity> entries = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entries.Select(x => new DecisionTableAuditEntry
        {
            Id = x.Id,
            TableId = x.TableId,
            Action = x.Action,
            Actor = x.Actor,
            Reason = x.Reason,
            Timestamp = x.Timestamp,
            PayloadJson = x.PayloadJson
        }).ToArray();
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return BulkDeleteAsync([id], reason: "single-delete", cancellationToken: cancellationToken);
    }

    private async Task UpsertCoreAsync(
        DecisionTableModel table,
        string? actor,
        string? reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DecisionTableRecordEntity? entity = await dbContext.Tables.FirstOrDefaultAsync(x => x.Id == table.Id, cancellationToken);
        bool isCreate = entity is null;

        entity ??= new DecisionTableRecordEntity();
        entity.Id = table.Id;

        DecisionTableModel snapshot = Clone(table);
        snapshot.CreatedAt = isCreate ? EnsureUtcTimestamp(table.CreatedAt, now) : entity.CreatedAt;
        snapshot.ModifiedAt = now;
        snapshot.Version = isCreate ? Math.Max(1, table.Version) : Math.Max(entity.Version + 1, table.Version + 1);
        snapshot.Rows = [.. snapshot.Rows.OrderBy(x => x.Order)];

        entity.Name = snapshot.Name;
        entity.Description = snapshot.Description;
        entity.HitPolicy = snapshot.HitPolicy.ToString();
        entity.TenantId = snapshot.TenantId;
        entity.Version = snapshot.Version;
        entity.PayloadJson = Serialize(snapshot);
        entity.CreatedAt = snapshot.CreatedAt;
        entity.ModifiedAt = snapshot.ModifiedAt;
        entity.IsDeleted = false;

        if (isCreate)
        {
            dbContext.Tables.Add(entity);
        }

        AddVersionEntry(snapshot, isCreate ? "create" : "update", actor, reason, now);
        AddAuditEntry(snapshot.Id, isCreate ? "create" : "update", actor, reason, Serialize(snapshot), now);
    }

    private void AddVersionEntry(
        DecisionTableModel table,
        string changeType,
        string? actor,
        string? reason,
        DateTimeOffset timestamp)
    {
        DecisionTableVersionEntity entity = new()
        {
            TableId = table.Id,
            Version = table.Version,
            ChangeType = changeType,
            Actor = actor,
            Reason = reason,
            Timestamp = timestamp,
            PayloadJson = Serialize(table)
        };
        dbContext.Versions.Add(entity);
    }

    private void AddAuditEntry(
        string? tableId,
        string action,
        string? actor,
        string? reason,
        string? payloadJson,
        DateTimeOffset timestamp)
    {
        DecisionTableAuditEntity entity = new()
        {
            TableId = tableId,
            Action = action,
            Actor = actor,
            Reason = reason,
            PayloadJson = payloadJson,
            Timestamp = timestamp
        };
        dbContext.AuditEntries.Add(entity);
    }

    private string Serialize(DecisionTableModel table)
    {
        return jsonSerializeService.Serialize(table);
    }

    private DecisionTableModel? Deserialize(string payloadJson)
    {
        return jsonSerializeService.Deserialize<DecisionTableModel>(payloadJson);
    }

    private DecisionTableModel Clone(DecisionTableModel table)
    {
        string json = Serialize(table);
        return Deserialize(json) ?? new DecisionTableModel();
    }

    private DecisionTableVersionSnapshot? MapVersion(DecisionTableVersionEntity entity)
    {
        DecisionTableModel? table = Deserialize(entity.PayloadJson);
        if (table is null)
        {
            return null;
        }

        return new DecisionTableVersionSnapshot
        {
            TableId = entity.TableId,
            Version = entity.Version,
            ChangeType = entity.ChangeType,
            Actor = entity.Actor,
            Reason = entity.Reason,
            Timestamp = entity.Timestamp,
            Table = table
        };
    }

    private static DateTimeOffset EnsureUtcTimestamp(DateTimeOffset timestamp, DateTimeOffset fallback)
    {
        return timestamp == default ? fallback : timestamp.ToUniversalTime();
    }
}
