

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Manages canary rollouts for ruleset versions.
/// </summary>
public sealed class CanaryRolloutService(
    RuleEngineDbContext dbContext,
    IRuleSetStore store,
    IRuleSetAuditStore auditStore,
    IRuleSetChangeNotifier? notifier = null,
    IMemoryCache? memoryCache = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : ICanaryRolloutService
{
    private static readonly TimeSpan CanaryLookupCacheTtl = TimeSpan.FromSeconds(30);
    private readonly ISystemExecutionContextAccessor _executionContext =
        executionContextAccessor ?? new SystemExecutionContextAccessor();

    /// <summary>Starts a canary rollout for a ruleset version.</summary>
    /// <param name="request">The canary rollout request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created rollout record.</returns>
    public async Task<CanaryRolloutRecord> StartCanaryAsync(
        StartCanaryRequest request,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);
        MGuard.NotEmpty(request.WorkflowName);
        MGuard.Against(request.Version <= 0, "Version must be greater than zero.");

        string tenantId = ResolveTenantId();
        string workflow = request.WorkflowName.Trim();
        string actor = string.IsNullOrWhiteSpace(request.StartedBy) ? "system" : request.StartedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if ((request.TargetTenantIds?.Length ?? 0) == 0 && !request.TargetPercentage.HasValue)
        {
            throw new MInternalException("StartCanary requires either TargetTenantIds or TargetPercentage.");
        }

        if (request.TargetPercentage.HasValue && (request.TargetPercentage < 1 || request.TargetPercentage > 99))
        {
            throw new MInternalException("TargetPercentage must be in range [1..99].");
        }

        RuleSetRecord? rule = await dbContext.RuleSets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Version == request.Version,
                cancellationToken) ?? throw new MNotFoundException("RuleSetVersion", $"{workflow}/v{request.Version}");
        if (rule.Status is RuleSetStatus.Draft or RuleSetStatus.PendingApproval or RuleSetStatus.Rejected)
        {
            throw new MInternalException(
                "Only Approved/Active/Superseded versions can be used for canary rollout.");
        }

        List<CanaryRolloutRecord> existing = await dbContext.CanaryRollouts
            .Where(x => x.TenantId == tenantId && x.WorkflowName == workflow && x.Status == CanaryStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (CanaryRolloutRecord active in existing)
        {
            active.Status = CanaryStatus.RolledBack;
            active.CompletedAt = now;
            active.RolledBackBy = actor;
            active.RollbackReason = "Superseded by new canary rollout.";
        }

        CanaryRolloutRecord record = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowName = workflow,
            Version = request.Version,
            TargetTenantIds = NormalizeTenants(request.TargetTenantIds),
            TargetPercentage = request.TargetPercentage,
            Status = CanaryStatus.Active,
            StartedAt = now,
            StartedBy = actor
        };

        dbContext.CanaryRollouts.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCanaryCache(tenantId, workflow);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = workflow,
                Action = "StartCanary",
                Version = request.Version,
                Actor = actor,
                Detail = record.TargetPercentage.HasValue
                    ? $"percentage:{record.TargetPercentage.Value}"
                    : $"tenants:{string.Join(",", record.TargetTenantIds)}"
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    workflow,
                    RuleSetChangeTypes.CanaryStarted,
                    request.Version,
                    now),
                cancellationToken);
        }

        return record;
    }

    /// <summary>Promotes an active canary rollout.</summary>
    /// <param name="rolloutId">Rollout identifier.</param>
    /// <param name="promotedBy">Actor performing the promotion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PromoteCanaryAsync(
        Guid rolloutId,
        string promotedBy,
        CancellationToken cancellationToken = default)
    {
        MGuard.Against(rolloutId == Guid.Empty, "Rollout id is required.");

        MGuard.NotEmpty(promotedBy);
        string tenantId = ResolveTenantId();
        string actor = promotedBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CanaryRolloutRecord rollout = await dbContext.CanaryRollouts
            .FirstOrDefaultAsync(x => x.Id == rolloutId && x.TenantId == tenantId, cancellationToken)
            ?? throw new MNotFoundException("CanaryRollout", rolloutId);
        if (rollout.Status != CanaryStatus.Active)
        {
            throw new MInternalException("Only active canary rollouts can be promoted.");
        }

        await store.SetActiveVersionAsync(rollout.WorkflowName, rollout.Version, cancellationToken);
        rollout.Status = CanaryStatus.Promoted;
        rollout.CompletedAt = now;
        rollout.PromotedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCanaryCache(tenantId, rollout.WorkflowName);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = rollout.WorkflowName,
                Action = "PromoteCanary",
                Version = rollout.Version,
                Actor = actor
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    rollout.WorkflowName,
                    RuleSetChangeTypes.CanaryPromoted,
                    rollout.Version,
                    now),
                cancellationToken);
        }
    }

    /// <summary>Rolls back an active canary rollout.</summary>
    /// <param name="rolloutId">Rollout identifier.</param>
    /// <param name="rolledBackBy">Actor performing the rollback.</param>
    /// <param name="reason">Reason for rollback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RollbackCanaryAsync(
        Guid rolloutId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        MGuard.Against(rolloutId == Guid.Empty, "Rollout id is required.");

        MGuard.NotEmpty(rolledBackBy);
        MGuard.NotEmpty(reason);
        string tenantId = ResolveTenantId();
        string actor = rolledBackBy.Trim();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CanaryRolloutRecord rollout = await dbContext.CanaryRollouts
            .FirstOrDefaultAsync(x => x.Id == rolloutId && x.TenantId == tenantId, cancellationToken)
            ?? throw new MNotFoundException("CanaryRollout", rolloutId);
        if (rollout.Status != CanaryStatus.Active)
        {
            throw new MInternalException("Only active canary rollouts can be rolled back.");
        }

        rollout.Status = CanaryStatus.RolledBack;
        rollout.CompletedAt = now;
        rollout.RolledBackBy = actor;
        rollout.RollbackReason = reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCanaryCache(tenantId, rollout.WorkflowName);

        await auditStore.AppendAsync(
            new RuleSetAuditEntry
            {
                TenantId = tenantId,
                WorkflowName = rollout.WorkflowName,
                Action = "RollbackCanary",
                Version = rollout.Version,
                Actor = actor,
                Detail = reason.Trim()
            },
            cancellationToken);
        if (notifier is not null)
        {
            await notifier.PublishAsync(
                new RuleSetChangeEvent(
                    tenantId,
                    rollout.WorkflowName,
                    RuleSetChangeTypes.CanaryRolledBack,
                    rollout.Version,
                    now),
                cancellationToken);
        }
    }

    /// <summary>Returns whether a canary version is active for a tenant.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a canary version applies to the tenant.</returns>
    public async Task<bool> IsCanaryActiveForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        int? version = await GetCanaryVersionForTenantAsync(workflowName, tenantId, cancellationToken);
        return version.HasValue;
    }

    /// <summary>Gets the canary version for a tenant if targeted.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The canary version or <c>null</c> if none applies.</returns>
    public async Task<int?> GetCanaryVersionForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(workflowName);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        string controlPlaneTenantId = ResolveTenantId();
        string workflow = workflowName.Trim();
        string targetTenant = tenantId.Trim();
        string marker = GetCanaryMarker(controlPlaneTenantId, workflow);
        string cacheKey = $"ruleset:canary:{controlPlaneTenantId}:{workflow}:{marker}:{targetTenant}".ToLowerInvariant();
        if (memoryCache is not null && memoryCache.TryGetValue(cacheKey, out int? cachedVersion))
        {
            return cachedVersion;
        }

        List<CanaryRolloutRecord> activeRollouts = await dbContext.CanaryRollouts
            .AsNoTracking()
            .Where(x =>
                x.TenantId == controlPlaneTenantId &&
                x.WorkflowName == workflow &&
                x.Status == CanaryStatus.Active)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);

        int? version = null;
        foreach (CanaryRolloutRecord rollout in activeRollouts)
        {
            if (IsTargeted(rollout, targetTenant))
            {
                version = rollout.Version;
                break;
            }
        }

        memoryCache?.Set(cacheKey, version, CanaryLookupCacheTtl);

        return version;
    }

    private static bool IsTargeted(CanaryRolloutRecord rollout, string tenantId)
    {
        if (rollout.TargetTenantIds.Length > 0)
        {
            return rollout.TargetTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
        }

        if (!rollout.TargetPercentage.HasValue)
        {
            return false;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId));
        int bucket = hash[0] % 100;
        return bucket < rollout.TargetPercentage.Value;
    }

    private void InvalidateCanaryCache(string controlPlaneTenantId, string workflowName)
    {
        if (memoryCache is null)
        {
            return;
        }

        string markerKey = $"ruleset:canary:marker:{controlPlaneTenantId}:{workflowName}".ToLowerInvariant();
        memoryCache.Set(markerKey, Guid.NewGuid().ToString("N"), CanaryLookupCacheTtl);
    }

    private string GetCanaryMarker(string controlPlaneTenantId, string workflowName)
    {
        if (memoryCache is null)
        {
            return "none";
        }

        string markerKey = $"ruleset:canary:marker:{controlPlaneTenantId}:{workflowName}".ToLowerInvariant();
        if (memoryCache.TryGetValue(markerKey, out string? marker) && !string.IsNullOrWhiteSpace(marker))
        {
            return marker;
        }

        marker = Guid.NewGuid().ToString("N");
        memoryCache.Set(markerKey, marker, CanaryLookupCacheTtl);
        return marker;
    }

    private static string[] NormalizeTenants(IEnumerable<string>? tenantIds)
    {
        if (tenantIds is null)
        {
            return [];
        }

        return [.. tenantIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private string ResolveTenantId()
    {
        string? tenantId = _executionContext.Get().TenantId;
        return string.IsNullOrWhiteSpace(tenantId)
            ? "default"
            : tenantId;
    }
}
