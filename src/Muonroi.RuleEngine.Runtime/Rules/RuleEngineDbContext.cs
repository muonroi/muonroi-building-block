using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Entity Framework DbContext for runtime ruleset storage.
/// </summary>
public sealed class RuleEngineDbContext(DbContextOptions<RuleEngineDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateImmutability();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Validates that modified <see cref="RuleSetRecord"/> entries do not violate immutability rules:
    /// <list type="bullet">
    ///   <item>Json content cannot be changed once a version reaches PendingApproval or beyond.</item>
    ///   <item>Status transitions must follow the allowed forward-only workflow path.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Json is mutated on a published version or an invalid status transition is attempted.
    /// </exception>
    private void ValidateImmutability()
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<RuleSetRecord> entry in ChangeTracker.Entries<RuleSetRecord>())
        {
            if (entry.State != Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                continue;
            }

            RuleSetStatus originalStatus = entry.OriginalValues.GetValue<RuleSetStatus>(nameof(RuleSetRecord.Status));

            // Guard 1: Json content is immutable once a version is published (>= PendingApproval, excluding Rejected)
            bool isPublishedStatus = originalStatus is
                RuleSetStatus.PendingApproval or
                RuleSetStatus.Approved or
                RuleSetStatus.Active or
                RuleSetStatus.Superseded;

            if (isPublishedStatus)
            {
                string originalJson = entry.OriginalValues.GetValue<string>(nameof(RuleSetRecord.Json));
                string currentJson = entry.CurrentValues.GetValue<string>(nameof(RuleSetRecord.Json));
                if (!string.Equals(originalJson, currentJson, StringComparison.Ordinal))
                {
                    throw new MInternalException(
                        $"Cannot modify Json of ruleset version with status '{originalStatus}'. Published versions are immutable.");
                }
            }

            // Guard 2: Status transitions must be forward-only along the approval workflow
            RuleSetStatus newStatus = entry.CurrentValues.GetValue<RuleSetStatus>(nameof(RuleSetRecord.Status));
            if (newStatus != originalStatus && !IsAllowedTransition(originalStatus, newStatus))
            {
                throw new MInternalException(
                    $"Invalid status transition from '{originalStatus}' to '{newStatus}' for ruleset version.");
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if transitioning from <paramref name="from"/> to <paramref name="to"/>
    /// is a valid forward workflow step or an explicitly permitted exception (e.g. rejection path).
    /// </summary>
    private static bool IsAllowedTransition(RuleSetStatus from, RuleSetStatus to) => (from, to) switch
    {
        // Submit for review
        (RuleSetStatus.Draft, RuleSetStatus.PendingApproval) => true,
        // Direct activate (no approval required)
        (RuleSetStatus.Draft, RuleSetStatus.Active) => true,
        // Approve
        (RuleSetStatus.PendingApproval, RuleSetStatus.Approved) => true,
        // Rejection path: PendingApproval -> Draft (RejectAsync sets back to Draft)
        (RuleSetStatus.PendingApproval, RuleSetStatus.Draft) => true,
        // Activate approved version
        (RuleSetStatus.Approved, RuleSetStatus.Active) => true,
        // Supersede when a new version becomes active
        (RuleSetStatus.Active, RuleSetStatus.Superseded) => true,
        // Resubmit after rejection via Draft -> PendingApproval (handled above)
        _ => false
    };

    /// <summary>Gets the rule set entities.</summary>
    public DbSet<RuleSetRecord> RuleSets => Set<RuleSetRecord>();

    /// <summary>Gets the canary rollout entities.</summary>
    public DbSet<CanaryRolloutRecord> CanaryRollouts => Set<CanaryRolloutRecord>();

    /// <summary>Gets the ruleset audit entities.</summary>
    public DbSet<RuleSetAuditRecord> RuleSetAudits => Set<RuleSetAuditRecord>();

    /// <summary>Gets the tenant rule assignment entities.</summary>
    public DbSet<TenantRuleAssignmentRecord> TenantRuleAssignments => Set<TenantRuleAssignmentRecord>();

    /// <summary>Gets the tenant quota override entities.</summary>
    public DbSet<TenantQuotaOverrideRecord> TenantQuotaOverrides => Set<TenantQuotaOverrideRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuleSetRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.WorkflowName, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkflowName, x.IsActive });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.WorkflowName).HasMaxLength(256);
            entity.Property(x => x.Json).HasColumnType("text");
            entity.Property(x => x.CreatedBy).HasMaxLength(256);
            entity.Property(x => x.SubmittedBy).HasMaxLength(256);
            entity.Property(x => x.ApprovedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedReason).HasColumnType("text");
        });

        modelBuilder.Entity<CanaryRolloutRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.WorkflowName, x.Status });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.WorkflowName).HasMaxLength(256);
            entity.Property(x => x.TargetTenantIds).HasColumnType("text[]");
            entity.Property(x => x.StartedBy).HasMaxLength(256);
            entity.Property(x => x.PromotedBy).HasMaxLength(256);
            entity.Property(x => x.RolledBackBy).HasMaxLength(256);
            entity.Property(x => x.RollbackReason).HasColumnType("text");
        });

        modelBuilder.Entity<RuleSetAuditRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.WorkflowName, x.OccurredAt });
            entity.HasIndex(x => x.EventType);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.WorkflowName).HasMaxLength(256);
            entity.Property(x => x.EventType).HasMaxLength(128);
            entity.Property(x => x.Actor).HasMaxLength(256);
            entity.Property(x => x.TargetTenantId).HasMaxLength(128);
            entity.Property(x => x.Detail).HasColumnType("text");
            entity.Property(x => x.ContentHash).HasMaxLength(256);
            entity.Property(x => x.SignatureAlgorithm).HasMaxLength(64);
            entity.Property(x => x.SignatureKeyId).HasMaxLength(128);
            entity.Property(x => x.Signature).HasColumnType("text");
        });

        modelBuilder.Entity<TenantRuleAssignmentRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.TargetTenantId, x.WorkflowName }).IsUnique();
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.TargetTenantId).HasMaxLength(128);
            entity.Property(x => x.WorkflowName).HasMaxLength(256);
            entity.Property(x => x.AssignedBy).HasMaxLength(256);
        });

        modelBuilder.Entity<TenantQuotaOverrideRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.TargetTenantId }).IsUnique();
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.TargetTenantId).HasMaxLength(128);
            entity.Property(x => x.UpdatedBy).HasMaxLength(256);
        });
    }
}
