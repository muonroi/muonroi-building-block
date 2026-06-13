using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    // Consolidation migration (Phase 10 / PROD-01). It replaces three migrations that had been
    // committed WITHOUT their EF .Designer.cs files and were therefore invisible to the migrator
    // (never applied): 20260325 AddRowLevelSecurityPolicies (original-5 RLS, USING-only),
    // 20260610 AddRuleLinkRequirementForeignKey (RuleLinks->Requirements FK), and
    // 20260611 AddCopilotDraftProvenance (provenance table + RLS). EF scaffolded the model ops
    // (CreateTable + index + FK) from the entity model; the raw RLS SQL below is re-injected by
    // hand because EF cannot derive RLS policies from the model. The original 5 tables now carry
    // tenant_isolation with BOTH USING and WITH CHECK (PROD-01 closes the cross-tenant write gap;
    // the legacy db/migrations/0002 raw SQL is a no-op on this PascalCase schema and is superseded).
    public partial class CatchUpRlsProvenanceWriteCheck : Migration
    {
        // Original 5 tenant-scoped tables that were USING-only (no policy on this DB) — backfilled
        // here with USING + WITH CHECK. The 4 traceability tables (Requirements, RuleLinks,
        // TestLinks, DryRunExamples) already carry USING + WITH CHECK from AddTraceabilityEntities.
        private static readonly string[] OriginalFiveTenantTables =
        [
            "RuleSets", "CanaryRollouts", "RuleSetAudits", "TenantRuleAssignments", "TenantQuotaOverrides"
        ];

        // All tenant-scoped tables whose RLS WITH CHECK this migration is responsible for asserting.
        private static readonly string[] AssertedTenantTables =
        [
            "RuleSets", "CanaryRollouts", "RuleSetAudits", "TenantRuleAssignments", "TenantQuotaOverrides",
            "CopilotDraftProvenance"
        ];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopilotDraftProvenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Workflow = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AiOriginalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AiOriginalSnapshot = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeneratedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EditedBeforeApproval = table.Column<bool>(type: "boolean", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopilotDraftProvenance", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopilotDraftProvenance_TenantId_Workflow_Version",
                table: "CopilotDraftProvenance",
                columns: new[] { "TenantId", "Workflow", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RuleLinks_Requirements_RequirementId",
                table: "RuleLinks",
                column: "RequirementId",
                principalTable: "Requirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ---- RLS re-injection (EF cannot derive policies from the model). ----
            // current_setting('app.current_tenant_id', true) is set by TenantRlsConnectionInterceptor
            // on connection open; the second arg (true) returns NULL (not throw) when unset, so the
            // equality fails and zero rows pass — fail-closed per ISO-01/D-04. "TenantId" is quoted so
            // pg_policies.tablename preserves the PascalCase the parity assertion matches against.

            // Original 5: enable RLS + tenant_isolation with USING AND WITH CHECK (PROD-01).
            foreach (string table in OriginalFiveTenantTables)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON ""{table}""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");
            }

            // New CopilotDraftProvenance table: RLS with USING + WITH CHECK (T-08-01).
            migrationBuilder.Sql(@"ALTER TABLE ""CopilotDraftProvenance"" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE ""CopilotDraftProvenance"" FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON ""CopilotDraftProvenance""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");

            // Table<->policy parity assertion: every asserted tenant-scoped table MUST carry a
            // tenant_isolation policy WITH a non-null WITH CHECK. Converts a silent fail-open into a
            // HARD migration failure (RAISE aborts the transaction). Runs on relational Postgres only.
            migrationBuilder.Sql(@"
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t, ', ') INTO missing
  FROM unnest(ARRAY['RuleSets','CanaryRollouts','RuleSetAudits','TenantRuleAssignments','TenantQuotaOverrides','CopilotDraftProvenance']) AS t
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
            // Reverse RLS first (CopilotDraftProvenance policy must drop before its table is dropped).
            foreach (string table in AssertedTenantTables)
            {
                migrationBuilder.Sql($@"DROP POLICY IF EXISTS tenant_isolation ON ""{table}"";");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropForeignKey(
                name: "FK_RuleLinks_Requirements_RequirementId",
                table: "RuleLinks");

            migrationBuilder.DropTable(
                name: "CopilotDraftProvenance");
        }
    }
}
