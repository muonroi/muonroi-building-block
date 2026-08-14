namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

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
    /// Validates that modified <see cref="RuleSetRecord"/> and <see cref="PdfTemplateVersionRecord"/>
    /// entries do not violate immutability rules:
    /// <list type="bullet">
    ///   <item>Content cannot be changed once a version reaches PendingApproval or beyond.</item>
    ///   <item>Status transitions must follow the allowed forward-only workflow path.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when content is mutated on a published version or an invalid status transition is attempted.
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
                    MGuard.State(false, 
                        $"Cannot modify Json of ruleset version with status '{originalStatus}'. Published versions are immutable.");
                }
            }

            // Guard 2: Status transitions must be forward-only along the approval workflow
            RuleSetStatus newStatus = entry.CurrentValues.GetValue<RuleSetStatus>(nameof(RuleSetRecord.Status));
            if (newStatus != originalStatus && !IsAllowedTransition(originalStatus, newStatus))
            {
                MGuard.State(false, 
                    $"Invalid status transition from '{originalStatus}' to '{newStatus}' for ruleset version.");
            }
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PdfTemplateVersionRecord> entry in ChangeTracker.Entries<PdfTemplateVersionRecord>())
        {
            if (entry.State != Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                continue;
            }

            PdfTemplateStatus originalStatus = entry.OriginalValues.GetValue<PdfTemplateStatus>(nameof(PdfTemplateVersionRecord.Status));

            // Guard 1: ContentJson is immutable once a PDF template version reaches PendingApproval or beyond
            bool isPublishedStatus = originalStatus is
                PdfTemplateStatus.PendingApproval or
                PdfTemplateStatus.Approved or
                PdfTemplateStatus.Active or
                PdfTemplateStatus.Superseded;

            if (isPublishedStatus)
            {
                string originalContent = entry.OriginalValues.GetValue<string>(nameof(PdfTemplateVersionRecord.ContentJson));
                string currentContent = entry.CurrentValues.GetValue<string>(nameof(PdfTemplateVersionRecord.ContentJson));
                if (!string.Equals(originalContent, currentContent, StringComparison.Ordinal))
                {
                    MGuard.State(false, 
                        $"Cannot modify ContentJson of PDF template version with status '{originalStatus}'. Published versions are immutable.");
                }
            }

            // Guard 2: Status transitions must be forward-only along the approval workflow
            PdfTemplateStatus newStatus = entry.CurrentValues.GetValue<PdfTemplateStatus>(nameof(PdfTemplateVersionRecord.Status));
            if (newStatus != originalStatus && !IsAllowedPdfTemplateTransition(originalStatus, newStatus))
            {
                MGuard.State(false, 
                    $"Invalid status transition from '{originalStatus}' to '{newStatus}' for PDF template version.");
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
        // Copilot-draft discard path (D-04 / VRF-02): a never-published Draft may be soft-rejected.
        // Rejected is terminal — no onward arm reaches Active, preserving the never-Active invariant (07-04 / T-08-02).
        (RuleSetStatus.Draft, RuleSetStatus.Rejected) => true,
        // Resubmit after rejection via Draft -> PendingApproval (handled above)
        _ => false
    };

    /// <summary>
    /// Returns <see langword="true"/> if transitioning a <see cref="PdfTemplateVersionRecord"/>
    /// from <paramref name="from"/> to <paramref name="to"/> is a valid forward workflow step.
    /// Mirrors <see cref="IsAllowedTransition"/> for PDF template lifecycle.
    /// </summary>
    private static bool IsAllowedPdfTemplateTransition(PdfTemplateStatus from, PdfTemplateStatus to) => (from, to) switch
    {
        // Submit for review
        (PdfTemplateStatus.Draft, PdfTemplateStatus.PendingApproval) => true,
        // Direct activate (no approval required)
        (PdfTemplateStatus.Draft, PdfTemplateStatus.Active) => true,
        // Approve
        (PdfTemplateStatus.PendingApproval, PdfTemplateStatus.Approved) => true,
        // Rejection path: PendingApproval -> Draft
        (PdfTemplateStatus.PendingApproval, PdfTemplateStatus.Draft) => true,
        // Activate approved version
        (PdfTemplateStatus.Approved, PdfTemplateStatus.Active) => true,
        // Supersede when a newer version becomes active
        (PdfTemplateStatus.Active, PdfTemplateStatus.Superseded) => true,
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

    /// <summary>Gets the PDF template header entities.</summary>
    public DbSet<PdfTemplateRecord> PdfTemplates => Set<PdfTemplateRecord>();

    /// <summary>Gets the PDF template versioned-content entities.</summary>
    public DbSet<PdfTemplateVersionRecord> PdfTemplateVersions => Set<PdfTemplateVersionRecord>();

    /// <summary>Gets the PDF template approval-event entities.</summary>
    public DbSet<PdfTemplateApprovalRecord> PdfTemplateApprovals => Set<PdfTemplateApprovalRecord>();

    // ── Traceability domain (Phase 3 — requirement ↔ rule ↔ test) ─────────────

    /// <summary>Gets the requirement entities.</summary>
    public DbSet<RequirementRecord> Requirements => Set<RequirementRecord>();

    /// <summary>Gets the rule-link entities (requirement ↔ rule).</summary>
    public DbSet<RuleLinkRecord> RuleLinks => Set<RuleLinkRecord>();

    /// <summary>Gets the test-link entities (rule ↔ test).</summary>
    public DbSet<TestLinkRecord> TestLinks => Set<TestLinkRecord>();

    /// <summary>Gets the dry-run example entities.</summary>
    public DbSet<DryRunExampleRecord> DryRunExamples => Set<DryRunExampleRecord>();

    /// <summary>Gets the copilot-draft provenance entities (Phase 8 — VRF-03 exit metric).</summary>
    public DbSet<CopilotDraftProvenanceRecord> CopilotDraftProvenance => Set<CopilotDraftProvenanceRecord>();

    /// <summary>Gets the ingested-source-document entities (Phase 15 — INGEST-01/02/03).</summary>
    public DbSet<IngestedSourceDocumentRecord> IngestedSourceDocuments => Set<IngestedSourceDocumentRecord>();

    /// <summary>Gets the outbound-sync job entities (Phase 17 — SYNC-02, tenant-scoped + retriable).</summary>
    public DbSet<OutboundSyncJobRecord> OutboundSyncJobs => Set<OutboundSyncJobRecord>();

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

        // ── PDF Template domain ──────────────────────────────────────────────

        modelBuilder.Entity<PdfTemplateRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Unique index: one header per (tenant, templateId)
            entity.HasIndex(x => new { x.TenantId, x.TemplateId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.TemplateId).HasMaxLength(256);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.CreatedBy).HasMaxLength(256);
            entity.Property(x => x.SubmittedBy).HasMaxLength(256);
            entity.Property(x => x.ApprovedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedReason).HasColumnType("text");
        });

        modelBuilder.Entity<PdfTemplateVersionRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Unique index: one row per (tenant, templateId, version) — mirrors RuleSetRecord
            entity.HasIndex(x => new { x.TenantId, x.TemplateId, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TemplateId, x.Status });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.TemplateId).HasMaxLength(256);
            entity.Property(x => x.ContentJson).HasColumnType("text");
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.CreatedBy).HasMaxLength(256);
            entity.Property(x => x.SubmittedBy).HasMaxLength(256);
            entity.Property(x => x.ApprovedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedReason).HasColumnType("text");
        });

        modelBuilder.Entity<PdfTemplateApprovalRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Index for fast lookup of all approval events for a version
            entity.HasIndex(x => new { x.TenantId, x.TemplateVersionId });
            entity.HasIndex(x => new { x.TenantId, x.TemplateId, x.Version });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.TemplateId).HasMaxLength(256);
            entity.Property(x => x.SubmittedBy).HasMaxLength(256);
            entity.Property(x => x.ApprovedBy).HasMaxLength(256);
            entity.Property(x => x.RejectedBy).HasMaxLength(256);
            entity.Property(x => x.RejectionReason).HasColumnType("text");
        });

        // ── Traceability domain (Phase 3) ───────────────────────────────────
        // These entities are intentionally mutable — no ValidateImmutability guard
        // (requirements can be edited, links can be deleted). See PATTERNS C-05/D-03.

        modelBuilder.Entity<RequirementRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Title });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Title).HasMaxLength(512);
            entity.Property(x => x.SourceSystem).HasMaxLength(128);
            entity.Property(x => x.SourceRef).HasMaxLength(1024);
            entity.Property(x => x.Approver).HasMaxLength(256);
            entity.Property(x => x.CreatedBy).HasMaxLength(256);
        });

        modelBuilder.Entity<RuleLinkRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Workflow });
            entity.HasIndex(x => x.RequirementId);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Workflow).HasMaxLength(256);
            entity.Property(x => x.NodeId).HasMaxLength(256);
            entity.Property(x => x.CreatedBy).HasMaxLength(256);
            // WR-01 (03-REVIEW.md): referential integrity for the requirement↔rule edge.
            // Without this FK an orphan RuleLink (RequirementId pointing at a deleted/non-existent
            // requirement) could be persisted and then silently dropped from the matrix
            // (TraceabilityMatrixBuilder.resolveRequirements) — an invisible hole. Cascade aligns
            // with D-01 (links are mutable/deletable): deleting a requirement removes its links
            // rather than leaving them dangling. The FK rides on the existing IX_RuleLinks_RequirementId.
            entity.HasOne<RequirementRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.RequirementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestLinkRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Workflow });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Workflow).HasMaxLength(256);
            entity.Property(x => x.NodeId).HasMaxLength(256);
            entity.Property(x => x.CaseId).HasMaxLength(256);
        });

        modelBuilder.Entity<DryRunExampleRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Workflow });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Workflow).HasMaxLength(256);
            entity.Property(x => x.NodeId).HasMaxLength(256);
            entity.Property(x => x.InputsJson).HasColumnType("text");
            entity.Property(x => x.ContextType).HasMaxLength(256);
            entity.Property(x => x.PromotedBy).HasMaxLength(256);
        });

        modelBuilder.Entity<CopilotDraftProvenanceRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Unique composite: one provenance row per (tenant, workflow, version) — supports the
            // activation O(1) lookup and prevents duplicate provenance rows (RESEARCH Pitfall 5).
            entity.HasIndex(x => new { x.TenantId, x.Workflow, x.Version }).IsUnique();
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Workflow).HasMaxLength(256);
            entity.Property(x => x.AiOriginalHash).HasMaxLength(64);
            entity.Property(x => x.AiOriginalSnapshot).HasColumnType("text");
            entity.Property(x => x.GeneratedBy).HasMaxLength(256);
        });

        modelBuilder.Entity<IngestedSourceDocumentRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Map to the singular table the Phase 15 migration + RLS policy created. The DbSet property is plural
            // ("IngestedSourceDocuments") and there is no pluralization convention here, so without this the EF
            // model targets a non-existent "IngestedSourceDocuments" table and any real-Postgres read 42P01s.
            entity.ToTable("IngestedSourceDocument");
            // Composite index: tenant + connector + sourceRef for fast per-tenant lookups (Phase 15 D-07).
            entity.HasIndex(x => new { x.TenantId, x.ConnectorId, x.SourceRef });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.ConnectorId).HasMaxLength(128);
            entity.Property(x => x.SourceRef).HasMaxLength(512);
            entity.Property(x => x.RedactedContent).HasColumnType("text");
            entity.Property(x => x.NormalizedContent).HasColumnType("text");
            entity.Property(x => x.CorrelationId).HasMaxLength(64);
            entity.Property(x => x.IngestedBy).HasMaxLength(256);
            // Phase 25 DOC-01: BA-confirmed classification ("rule" | "business" | null). No RLS change —
            // this is a column add on the already-secured IngestedSourceDocument table.
            entity.Property(x => x.DocType).HasMaxLength(16);
        });

        modelBuilder.Entity<OutboundSyncJobRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            // Map to the singular table the migration + RLS policy + parity DO-block reference. Without this
            // explicit name EF Core defaults to the DbSet property name ("OutboundSyncJobs", plural) — there is
            // NO global pluralization convention in this context — which 42P01-crashes the background processor.
            entity.ToTable("OutboundSyncJob");
            // Index on (tenant, status) — the processor claims Pending/retriable rows per tenant (Phase 17 D-03).
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.Workflow).HasMaxLength(256);
            entity.Property(x => x.Approver).HasMaxLength(256);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.LastError).HasColumnType("text");
        });
    }
}
