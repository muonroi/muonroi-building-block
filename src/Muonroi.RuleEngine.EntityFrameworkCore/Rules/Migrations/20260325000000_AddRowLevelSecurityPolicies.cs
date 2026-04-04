using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurityPolicies : Migration
    {
        // Tenant-scoped tables that require RLS policies.
        private static readonly string[] TenantScopedTables =
        [
            "RuleSets",
            "CanaryRollouts",
            "RuleSetAudits",
            "TenantRuleAssignments",
            "TenantQuotaOverrides"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable PostgreSQL Row-Level Security on every tenant-scoped table.
            // FORCE ROW LEVEL SECURITY ensures the policy applies even to the table owner.
            // The policy uses current_setting('app.current_tenant_id', true) which is set by
            // TenantRlsConnectionInterceptor on every connection open when RLS is enabled.
            // The second argument (true) makes current_setting return NULL (not throw) when the
            // setting has not been set — matching the empty-string fallback from the interceptor.
            foreach (string table in TenantScopedTables)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON ""{table}""
    USING (""TenantId"" = current_setting('app.current_tenant_id', true));");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove RLS policies and disable row-level security on rollback.
            foreach (string table in TenantScopedTables)
            {
                migrationBuilder.Sql($@"DROP POLICY IF EXISTS tenant_isolation ON ""{table}"";");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
