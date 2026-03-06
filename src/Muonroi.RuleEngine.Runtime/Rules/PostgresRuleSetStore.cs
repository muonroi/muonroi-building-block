using Microsoft.EntityFrameworkCore.Storage;

namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class PostgresRuleSetStore(
    RuleEngineDbContext dbContext,
    RuleControlPlaneOptions? controlPlaneOptions = null) : IRuleSetStore
{
    private readonly RuleControlPlaneOptions _options = controlPlaneOptions ?? new RuleControlPlaneOptions();

    public async Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentNullException.ThrowIfNull(json);

        string tenantId = ResolveTenantId();
        string normalizedWorkflow = workflowName.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using IDbContextTransaction? transaction = await TryBeginTransactionAsync(cancellationToken);

        int? maxVersion = await dbContext.RuleSets
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow)
            .MaxAsync(x => (int?)x.Version, cancellationToken);
        int nextVersion = (maxVersion ?? 0) + 1;

        RuleSetRecord record = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowName = normalizedWorkflow,
            Version = nextVersion,
            Json = json,
            Status = _options.RequireApproval ? RuleSetStatus.Draft : RuleSetStatus.Active,
            IsActive = !_options.RequireApproval,
            CreatedBy = "system",
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!_options.RequireApproval)
        {
            record.ApprovedBy = "system";
            record.ApprovedAt = now;

            List<RuleSetRecord> active = await dbContext.RuleSets
                .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (RuleSetRecord item in active)
            {
                item.IsActive = false;
                if (item.Status == RuleSetStatus.Active)
                {
                    item.Status = RuleSetStatus.Superseded;
                }

                item.UpdatedAt = now;
            }
        }

        dbContext.RuleSets.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<string?> GetAsync(
        string workflowName,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);

        string tenantId = ResolveTenantId();
        string normalizedWorkflow = workflowName.Trim();

        IQueryable<RuleSetRecord> query = dbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow);

        RuleSetRecord? record;
        if (version.HasValue)
        {
            record = await query.FirstOrDefaultAsync(x => x.Version == version.Value, cancellationToken);
        }
        else
        {
            record = await query
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return record?.Json;
    }

    public async Task SetActiveVersionAsync(
        string workflowName,
        int version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        string tenantId = ResolveTenantId();
        string normalizedWorkflow = workflowName.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using IDbContextTransaction? transaction = await TryBeginTransactionAsync(cancellationToken);

        RuleSetRecord? target = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow && x.Version == version,
                cancellationToken);
        if (target is null)
        {
            throw new InvalidOperationException(
                $"Ruleset version '{version}' was not found for workflow '{normalizedWorkflow}'.");
        }

        if (_options.RequireApproval &&
            target.Status is RuleSetStatus.Draft or RuleSetStatus.PendingApproval or RuleSetStatus.Rejected)
        {
            throw new InvalidOperationException(
                "Only approved ruleset versions can be activated when approval workflow is enabled.");
        }

        List<RuleSetRecord> active = await dbContext.RuleSets
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (RuleSetRecord item in active)
        {
            if (item.Id == target.Id)
            {
                continue;
            }

            item.IsActive = false;
            if (item.Status == RuleSetStatus.Active)
            {
                item.Status = RuleSetStatus.Superseded;
            }

            item.UpdatedAt = now;
        }

        target.IsActive = true;
        target.Status = RuleSetStatus.Active;
        target.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        string tenantId = ResolveTenantId();
        string normalizedWorkflow = workflowName.Trim();

        int[] versions = await dbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow)
            .OrderBy(x => x.Version)
            .Select(x => x.Version)
            .ToArrayAsync(cancellationToken);
        return versions;
    }

    public async Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        string tenantId = ResolveTenantId();
        string normalizedWorkflow = workflowName.Trim();

        int? active = await dbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow && x.IsActive)
            .OrderByDescending(x => x.Version)
            .Select(x => (int?)x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (active.HasValue)
        {
            return active;
        }

        return await dbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WorkflowName == normalizedWorkflow)
            .OrderByDescending(x => x.Version)
            .Select(x => (int?)x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        string tenantId = ResolveTenantId();
        List<string> workflows = await dbContext.RuleSets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.WorkflowName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        return workflows;
    }

    private static string ResolveTenantId()
    {
        return string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)
            ? "default"
            : TenantContext.CurrentTenantId!;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException) when (!dbContext.Database.IsRelational())
        {
            return null;
        }
    }
}
