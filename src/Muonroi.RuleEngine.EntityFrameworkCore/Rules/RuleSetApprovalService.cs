namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Implements approval workflow transitions for rulesets.
/// </summary>
public sealed class RuleSetApprovalService(
    RuleEngineDbContext dbContext,
    IRuleSetAuditStore auditStore,
    IRuleSetChangeNotifier? notifier = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : IRuleSetApprovalService
{
    private readonly ISystemExecutionContextAccessor _executionContext =
        executionContextAccessor ?? new SystemExecutionContextAccessor();

    /// <inheritdoc />
    public async Task<RuleSetRecord> SubmitForApprovalAsync(
        string workflowName,
        int version,
        string submittedBy,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(workflowName);
        MGuard.NotEmpty(submittedBy);
        MGuard.Against(version <= 0, "Version must be greater than zero.");

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = submittedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{version}");

        if (record.Status is not RuleSetStatus.Draft and not RuleSetStatus.Rejected)
        {
            throw new MInternalException(
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

    /// <inheritdoc />
    public async Task<RuleSetRecord> ApproveAsync(
        string workflowName,
        int version,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(workflowName);
        MGuard.NotEmpty(approvedBy);
        MGuard.Against(version <= 0, "Version must be greater than zero.");

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = approvedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{version}");

        if (record.Status != RuleSetStatus.PendingApproval)
        {
            throw new MInternalException(
                $"Ruleset '{workflow}' v{version} cannot be approved from status '{record.Status}'.");
        }

        if (!string.IsNullOrWhiteSpace(record.SubmittedBy) &&
            string.Equals(record.SubmittedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new MInternalException("Maker-checker violation: submitter cannot approve their own ruleset.");
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

    /// <inheritdoc />
    public async Task<RuleSetRecord> RejectAsync(
        string workflowName,
        int version,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(workflowName);
        MGuard.NotEmpty(rejectedBy);
        MGuard.NotEmpty(reason);
        MGuard.Against(version <= 0, "Version must be greater than zero.");

        string tenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string actor = rejectedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RuleSetRecord record = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == version,
                cancellationToken)
            ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{version}");

        if (record.Status != RuleSetStatus.PendingApproval)
        {
            throw new MInternalException(
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

    private string ResolveTenantId()
    {
        string? tenantId = _executionContext.Get().TenantId;
        return string.IsNullOrWhiteSpace(tenantId)
            ? "default"
            : tenantId;
    }
}

