namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Entity Framework DbContext for runtime ruleset storage.
/// </summary>
public sealed class RuleEngineDbContext(DbContextOptions<RuleEngineDbContext> options) : DbContext(options)
{
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
