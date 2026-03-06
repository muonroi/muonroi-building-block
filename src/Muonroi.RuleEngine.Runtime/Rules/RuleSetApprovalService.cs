namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetApprovalService(
    RuleEngineDbContext dbContext,
    IRuleSetAuditStore auditStore,
    IRuleSetChangeNotifier? notifier = null) : IRuleSetApprovalService
{
    public async Task<RuleSetRecord> SubmitForApprovalAsync(
        string workflowName,
        int version,
        string submittedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = submittedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Ruleset version '{version}' for workflow '{workflow}' was not found.");

        if (record.Status is not RuleSetStatus.Draft and not RuleSetStatus.Rejected)
        {
            throw new InvalidOperationException(
                $"Ruleset '{workflow}' v{version} cannot be submitted from status '{record.Status}'.");
        }

        record.Status = RuleSetStatus.PendingApproval;
        record.SubmittedBy = actor;
        record.SubmittedAt = now;
        record.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = workflow,
                Action = "SubmitForApproval",
                Version = version,
                Actor = actor
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    workflow,
                    RuleSetChangeTypes.SubmittedForApproval,
                    version,
                    now),
                cancellationToken);
        }

        return record;
    }

    public async Task<RuleSetRecord> ApproveAsync(
        string workflowName,
        int version,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = approvedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Ruleset version '{version}' for workflow '{workflow}' was not found.");

        if (record.Status != RuleSetStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Ruleset '{workflow}' v{version} cannot be approved from status '{record.Status}'.");
        }

        if (!string.IsNullOrWhiteSpace(record.SubmittedBy) &&
            string.Equals(record.SubmittedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Maker-checker violation: submitter cannot approve their own ruleset.");
        }

        record.Status = RuleSetStatus.Approved;
        record.ApprovedBy = actor;
        record.ApprovedAt = now;
        record.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = workflow,
                Action = "Approve",
                Version = version,
                Actor = actor
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    workflow,
                    RuleSetChangeTypes.Approved,
                    version,
                    now),
                cancellationToken);
        }

        return record;
    }

    public async Task<RuleSetRecord> RejectAsync(
        string workflowName,
        int version,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = rejectedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Ruleset version '{version}' for workflow '{workflow}' was not found.");

        if (record.Status != RuleSetStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Ruleset '{workflow}' v{version} cannot be rejected from status '{record.Status}'.");
        }

        record.Status = RuleSetStatus.Draft;
        record.RejectedBy = actor;
        record.RejectedReason = reason.Trim();
        record.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = workflow,
                Action = "Reject",
                Version = version,
                Actor = actor,
                Detail = reason.Trim()
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    workflow,
                    RuleSetChangeTypes.Rejected,
                    version,
                    now),
                cancellationToken);
        }

        return record;
    }

    private static string ResolveTenantId()
    {
        return string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)
            ? "default"
            : TenantContext.CurrentTenantId!;
    }
}

