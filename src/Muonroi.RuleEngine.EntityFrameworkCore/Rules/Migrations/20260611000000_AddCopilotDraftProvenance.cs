#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddCopilotDraftProvenance : Migration
    {
        // C-02 / T-08-01: RLS does NOT auto-cover new tables. CopilotDraftProvenance is registered
        // here as tenant-scoped and driven through the ENABLE/FORCE/CREATE POLICY loop below, then
        // asserted by the parity DO-block and the control-plane RlsPoliciesTests (defence-in-depth).
        // A table absent from this array would silently fail open — dotnet build cannot catch that.
        //
        // A1 (pg_policies.tablename casing): Postgres lowercases only UNQUOTED identifiers. EF Core
        // creates this table with QUOTED PascalCase DDL (CREATE TABLE "CopilotDraftProvenance") and
        // the CREATE POLICY targets the quoted form, so pg_policies.tablename PRESERVES the
        // PascalCase. The parity DO-block and RlsPoliciesTests therefore match the PascalCase name.
        private static readonly string[] TenantScopedTables =
        [
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

            // Unique composite (RESEARCH Pitfall 5): one provenance row per (tenant, workflow,
            // version); supports the activation O(1) lookup and prevents duplicate provenance rows.
            migrationBuilder.CreateIndex(
                name: "IX_CopilotDraftProvenance_TenantId_Workflow_Version",
                table: "CopilotDraftProvenance",
                columns: new[] { "TenantId", "Workflow", "Version" },
                unique: true);

            // Row-Level Security for the new tenant-scoped table.
            // FORCE ROW LEVEL SECURITY makes the policy apply even to the table owner.
            // current_setting('app.current_tenant_id', true) is set by TenantRlsConnectionInterceptor
            // on connection open; the second argument (true) makes current_setting return NULL (not
            // throw) when unset — matching the interceptor fallback.
            //
            // Every policy carries BOTH USING (controls SELECT/DELETE visibility) AND WITH CHECK
            // (constrains INSERT/UPDATE row creation) on the same tenant condition. WITH CHECK closes
            // the cross-tenant write vector (T-08-01).
            foreach (string table in TenantScopedTables)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON ""{table}""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true))
    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true));");
            }

            // Table↔policy parity assertion (mirrors AddTraceabilityEntities CR-02). The CREATE POLICY
            // loop above is the ONLY place RLS is attached. A future divergence (table renamed in Up()
            // but not in TenantScopedTables, or a new table forgotten) would ship a table fail-open
            // silently — dotnet build cannot catch that. This converts the silent fail-open into a HARD
            // migration failure: it RAISEs (aborting the migration) if the scoped table lacks a
            // tenant_isolation policy in pg_policies. Catalog names are matched in the PascalCase form
            // per the A1 convention. Runs only on relational Postgres; the EF InMemory provider used by
            // the EFCore test project cannot execute it (covered by control-plane RlsPoliciesTests).
            migrationBuilder.Sql(@"
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t, ', ') INTO missing
  FROM unnest(ARRAY['CopilotDraftProvenance']) AS t
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
                name: "CopilotDraftProvenance");
        }
    }
}
