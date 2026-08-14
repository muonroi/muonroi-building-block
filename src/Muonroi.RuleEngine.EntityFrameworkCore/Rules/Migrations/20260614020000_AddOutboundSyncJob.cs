#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    // Phase 17 / D-03 (SYNC-02). Creates the OutboundSyncJob table with RLS ENABLE+FORCE and a
    // tenant_isolation policy carrying BOTH USING and WITH CHECK keyed on
    // current_setting('app.current_tenant_id', true) — the WITH CHECK requirement. A USING-only
    // policy is the PROD-01 regression (cross-tenant writes silently succeed); both predicates are
    // mandatory and identical. The pg_policies parity DO-block at the end of Up() extends the asserted
    // ARRAY to include 'OutboundSyncJob' so the migration RAISEs at apply time if the WITH CHECK
    // policy is absent for any tenant-scoped table (T-17-02 regression guard).
    //
    // The background processor (plan 17-03) reads/writes through the TenantRlsConnectionInterceptor
    // with app.current_tenant_id set from the row's TenantId; this table holds no document content —
    // LastError is HTTP-status + truncated body, written by the processor (T-17-04, schema only).
    public partial class AddOutboundSyncJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboundSyncJob",
                columns: table => new
                {
                    Id               = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId         = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Workflow         = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version          = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Approver         = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EnqueuedAtUtc    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status           = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount     = table.Column<int>(type: "integer", nullable: false),
                    LastError        = table.Column<string>(type: "text", nullable: true),
                    LastAttemptUtc   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRetryUtc     = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundSyncJob", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundSyncJob_TenantId_Status",
                table: "OutboundSyncJob",
                columns: new[] { "TenantId", "Status" });

            // ---- RLS injection (D-03). EF cannot derive RLS policies from the model. ----
            // current_setting('app.current_tenant_id', true) is set by TenantRlsConnectionInterceptor
            // on connection open; the second arg (true) returns NULL (not throw) when unset, so the
            // equality fails and zero rows pass — fail-closed. "TenantId" is quoted so
            // pg_policies.tablename preserves the PascalCase the parity assertion matches against.

            migrationBuilder.Sql(@"ALTER TABLE ""OutboundSyncJob"" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE ""OutboundSyncJob"" FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON ""OutboundSyncJob""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");

            // Table<->policy parity assertion: every asserted tenant-scoped table MUST carry a
            // tenant_isolation policy WITH a non-null WITH CHECK. Converts a silent fail-open into a
            // HARD migration failure (RAISE aborts the transaction). Extends the existing ARRAY from
            // AddIngestedSourceDocument to include OutboundSyncJob (T-17-02).
            migrationBuilder.Sql(@"
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t, ', ') INTO missing
  FROM unnest(ARRAY['RuleSets','CanaryRollouts','RuleSetAudits','TenantRuleAssignments',
    'TenantQuotaOverrides','Requirements','RuleLinks','TestLinks','DryRunExamples',
    'CopilotDraftProvenance','IngestedSourceDocument','OutboundSyncJob']) AS t
  WHERE NOT EXISTS (
    SELECT 1 FROM pg_policies p
    WHERE p.tablename = t AND p.policyname = 'tenant_isolation' AND p.with_check IS NOT NULL);
  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'RLS tenant_isolation WITH CHECK missing for tenant-scoped tables: %', missing;
  END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop policy before table — policy references table; ordering matters.
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS tenant_isolation ON ""OutboundSyncJob"";");
            migrationBuilder.Sql(@"ALTER TABLE ""OutboundSyncJob"" DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.DropTable(name: "OutboundSyncJob");
        }
    }
}
