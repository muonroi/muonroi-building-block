using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceabilityEntities : Migration
    {
        // C-02 / MTX-01: RLS does NOT auto-cover new tables. Every traceability table is
        // registered here as tenant-scoped and driven through the ENABLE/FORCE/CREATE POLICY
        // loop below. This migration-local array mirrors the pattern in
        // 20260325000000_AddRowLevelSecurityPolicies.cs (lines 11-18). A new table absent from
        // this array would silently fail open — dotnet build cannot catch that, so the names
        // are grep-verified in the 03-01 plan acceptance criteria and asserted by RlsPoliciesTests.
        //
        // A1 (pg_policies.tablename casing): Postgres stores unquoted catalog identifiers
        // lowercased. EF Core creates tables with quoted PascalCase DDL (e.g. "Requirements"),
        // but pg_policies.tablename stores the unquoted lowercase form ("requirements"). The
        // 03-01 Task 3 RlsPoliciesTests query therefore matches against the lowercase names
        // ("requirements", "rulelinks", "testlinks", "dryrunexamples"). If a live-DB run shows
        // otherwise, adjust the test's expected-table casing.
        private static readonly string[] TenantScopedTables =
        [
            "Requirements",
            "RuleLinks",
            "TestLinks",
            "DryRunExamples"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DryRunExamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Workflow = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    InputsJson = table.Column<string>(type: "text", nullable: false),
                    ContextType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PromotedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PromotedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DryRunExamples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceRef = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Approver = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Workflow = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Workflow = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CaseId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DryRunExamples_TenantId_Workflow",
                table: "DryRunExamples",
                columns: new[] { "TenantId", "Workflow" });

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_TenantId_Title",
                table: "Requirements",
                columns: new[] { "TenantId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleLinks_RequirementId",
                table: "RuleLinks",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleLinks_TenantId_Workflow",
                table: "RuleLinks",
                columns: new[] { "TenantId", "Workflow" });

            migrationBuilder.CreateIndex(
                name: "IX_TestLinks_TenantId_Workflow",
                table: "TestLinks",
                columns: new[] { "TenantId", "Workflow" });

            // Row-Level Security for every new tenant-scoped table.
            // FORCE ROW LEVEL SECURITY makes the policy apply even to the table owner.
            // The policy uses current_setting('app.current_tenant_id', true) which is set by
            // TenantRlsConnectionInterceptor on connection open; the second argument (true) makes
            // current_setting return NULL (not throw) when unset — matching the interceptor fallback.
            //
            // CRITICAL CORRECTION vs 20260325000000_AddRowLevelSecurityPolicies.cs (which uses
            // USING only and is therefore fail-open on INSERT/UPDATE): every policy here carries
            // BOTH USING (controls SELECT/DELETE visibility) AND WITH CHECK (constrains
            // INSERT/UPDATE row creation) on the same tenant condition. WITH CHECK closes the
            // cross-tenant write vector (T-03-01 / MTX-01).
            foreach (string table in TenantScopedTables)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON ""{table}""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");
            }

            // CR-02 (03-REVIEW.md): table↔policy parity assertion. The CREATE POLICY loop above is
            // the ONLY place RLS is attached to the four tenant-scoped tables. A future divergence
            // (a table renamed in Up() but not in TenantScopedTables, or a new table added but
            // forgotten) would ship a table fail-open (world-readable across tenants) silently —
            // dotnet build cannot catch that. This final statement of Up() converts that silent
            // fail-open into a HARD migration failure: it RAISEs (aborting the migration) if any of
            // the four scoped tables lacks a tenant_isolation policy in pg_policies. Catalog names
            // are matched lowercase per the A1 convention documented in the header (lines 18-23) —
            // pg_policies.tablename stores the unquoted lowercase form. This runs only on relational
            // Postgres; the EF InMemory provider used by the EFCore test project cannot execute it
            // (covered instead by control-plane RlsPoliciesTests against real Postgres).
            migrationBuilder.Sql(@"
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t, ', ') INTO missing
  FROM unnest(ARRAY['requirements','rulelinks','testlinks','dryrunexamples']) AS t
  WHERE NOT EXISTS (
    SELECT 1 FROM pg_policies p
    WHERE p.tablename = t AND p.policyname = 'tenant_isolation');
  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'RLS policy missing for tenant-scoped tables: %', missing;
  END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror the Up RLS setup: drop policies and disable RLS before dropping tables.
            foreach (string table in TenantScopedTables)
            {
                migrationBuilder.Sql($@"DROP POLICY IF EXISTS tenant_isolation ON ""{table}"";");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropTable(
                name: "DryRunExamples");

            migrationBuilder.DropTable(
                name: "Requirements");

            migrationBuilder.DropTable(
                name: "RuleLinks");

            migrationBuilder.DropTable(
                name: "TestLinks");
        }
    }
}
