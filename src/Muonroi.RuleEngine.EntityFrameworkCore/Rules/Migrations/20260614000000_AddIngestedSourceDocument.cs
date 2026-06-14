using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    // Phase 15 / INGEST-01. Creates the IngestedSourceDocument table with RLS ENABLE+FORCE and a
    // tenant_isolation policy carrying BOTH USING and WITH CHECK keyed on
    // current_setting('app.current_tenant_id', true) — the D-08 WITH CHECK requirement. A USING-only
    // policy is the PROD-01 regression (cross-tenant writes silently succeed); both predicates are
    // mandatory and identical. The pg_policies parity DO-block at the end of Up() extends the asserted
    // ARRAY to include 'IngestedSourceDocument' so the migration RAISEs at apply time if the WITH CHECK
    // policy is absent for any tenant-scoped table (T-15-02 regression guard).
    //
    // No raw-body column exists on this table — only RedactedContent (redacted artifact) and
    // NormalizedContent (plaintext before redaction). Raw document body is structurally impossible to
    // persist here (T-15-07 / D-04/D-06).
    public partial class AddIngestedSourceDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestedSourceDocument",
                columns: table => new
                {
                    Id            = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId      = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConnectorId   = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceRef     = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RedactedContent  = table.Column<string>(type: "text", nullable: false),
                    NormalizedContent = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IngestedAt    = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestedBy    = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestedSourceDocument", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngestedSourceDocument_TenantId_ConnectorId_SourceRef",
                table: "IngestedSourceDocument",
                columns: new[] { "TenantId", "ConnectorId", "SourceRef" });

            // ---- RLS injection (D-08). EF cannot derive RLS policies from the model. ----
            // current_setting('app.current_tenant_id', true) is set by TenantRlsConnectionInterceptor
            // on connection open; the second arg (true) returns NULL (not throw) when unset, so the
            // equality fails and zero rows pass — fail-closed per ISO-01/D-04. "TenantId" is quoted so
            // pg_policies.tablename preserves the PascalCase the parity assertion matches against.

            migrationBuilder.Sql(@"ALTER TABLE ""IngestedSourceDocument"" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE ""IngestedSourceDocument"" FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON ""IngestedSourceDocument""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");

            // Table<->policy parity assertion: every asserted tenant-scoped table MUST carry a
            // tenant_isolation policy WITH a non-null WITH CHECK. Converts a silent fail-open into a
            // HARD migration failure (RAISE aborts the transaction). Extends the existing ARRAY from
            // CatchUpRlsProvenanceWriteCheck to include IngestedSourceDocument (T-15-02).
            migrationBuilder.Sql(@"
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t, ', ') INTO missing
  FROM unnest(ARRAY['RuleSets','CanaryRollouts','RuleSetAudits','TenantRuleAssignments',
    'TenantQuotaOverrides','Requirements','RuleLinks','TestLinks','DryRunExamples',
    'CopilotDraftProvenance','IngestedSourceDocument']) AS t
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
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS tenant_isolation ON ""IngestedSourceDocument"";");
            migrationBuilder.Sql(@"ALTER TABLE ""IngestedSourceDocument"" DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.DropTable(name: "IngestedSourceDocument");
        }
    }
}
