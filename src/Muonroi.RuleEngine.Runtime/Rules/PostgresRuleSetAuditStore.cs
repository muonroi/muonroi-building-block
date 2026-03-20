namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Database-backed audit store for ruleset governance actions.
/// </summary>
public sealed class PostgresRuleSetAuditStore(
    RuleEngineDbContext dbContext,
    IRuleSetAuditSigner signer,
    IMJsonSerializeService jsonSerializeService,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : IRuleSetAuditStore
{
    private readonly ISystemExecutionContextAccessor _executionContext =
        executionContextAccessor ?? new SystemExecutionContextAccessor();

    /// <inheritdoc />
    public async Task AppendAsync(RuleSetAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string tenantId = string.IsNullOrWhiteSpace(entry.TenantId) ? ResolveTenantId() : entry.TenantId.Trim();
        DateTimeOffset occurredAt = entry.TimestampUtc == default ? DateTimeOffset.UtcNow : entry.TimestampUtc;
        string actor = string.IsNullOrWhiteSpace(entry.Actor) ? "system" : entry.Actor.Trim();
        string workflow = entry.WorkflowName?.Trim() ?? string.Empty;
        string? targetTenantId = string.IsNullOrWhiteSpace(entry.TargetTenantId) ? null : entry.TargetTenantId.Trim();
        string eventType = string.IsNullOrWhiteSpace(entry.Action) ? "unknown" : entry.Action.Trim();
        string detail = entry.Detail ?? string.Empty;

        string hashPayload = jsonSerializeService.Serialize(new
        {
            TenantId = tenantId,
            WorkflowName = workflow,
            TargetTenantId = targetTenantId,
            Version = entry.Version,
            EventType = eventType,
            Actor = actor,
            Detail = detail,
            OccurredAt = occurredAt
        });
        string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashPayload)));
        string signaturePayload = BuildSignaturePayload(
            tenantId,
            workflow,
            entry.Version,
            eventType,
            actor,
            occurredAt,
            contentHash);
        string signature = signer.Sign(signaturePayload);

        RuleSetAuditRecord record = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowName = workflow,
            TargetTenantId = targetTenantId,
            Version = entry.Version,
            EventType = eventType,
            Actor = actor,
            Detail = string.IsNullOrWhiteSpace(entry.Detail) ? null : entry.Detail,
            OccurredAt = occurredAt,
            ContentHash = contentHash,
            Signature = signature,
            SignatureAlgorithm = signer.SignatureAlgorithm,
            SignatureKeyId = signer.KeyId
        };

        dbContext.RuleSetAudits.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuleSetAuditPage> QueryAsync(
        string? workflowName = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        string tenantId = ResolveTenantId();
        IQueryable<RuleSetAuditRecord> query = dbContext.RuleSetAudits
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(workflowName))
        {
            string workflow = workflowName.Trim();
            query = query.Where(x => x.WorkflowName == workflow);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<RuleSetAuditRecord> rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<RuleSetAuditEntry> entries = [.. rows.Select(row => new RuleSetAuditEntry
        {
            Id = row.Id.ToString("N"),
            TenantId = row.TenantId,
            WorkflowName = row.WorkflowName,
            TargetTenantId = row.TargetTenantId,
            Action = row.EventType,
            Version = row.Version,
            Actor = row.Actor,
            Detail = row.Detail,
            TimestampUtc = row.OccurredAt,
            ContentHash = row.ContentHash,
            Signature = row.Signature,
            SignatureAlgorithm = row.SignatureAlgorithm,
            SignatureKeyId = row.SignatureKeyId
        })];

        return new RuleSetAuditPage
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = entries
        };
    }

    private static string BuildSignaturePayload(
        string tenantId,
        string workflowName,
        int? version,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        string contentHash)
    {
        return $"{tenantId}|{workflowName}|{version}|{eventType}|{actor}|{occurredAt:O}|{contentHash}";
    }

    private string ResolveTenantId()
    {
        string? tenantId = _executionContext.Get().TenantId;
        return string.IsNullOrWhiteSpace(tenantId)
            ? "default"
            : tenantId;
    }
}
