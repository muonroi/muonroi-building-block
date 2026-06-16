using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Muonroi.RuleEngine.EntityFrameworkCore.Rules;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    // Phase 25 / DOC-01. Adds the nullable DocType column ("rule" | "business" | null) to the existing
    // IngestedSourceDocument table. This is a COLUMN ADD on an already RLS-secured table — no new
    // tenant_isolation policy, no ENABLE/FORCE, and NO change to the pg_policies parity ARRAY (the table
    // is already asserted by 20260614000000_AddIngestedSourceDocument). The column is BA-written from the
    // import-time classification choice; a deterministic heuristic only SUGGESTS it (no AI). Legacy rows
    // stay NULL and are treated as rule by default downstream.
    [DbContext(typeof(RuleEngineDbContext))]
    [Migration("20260616000000_AddDocTypeToIngestedSource")]
    public partial class AddDocTypeToIngestedSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocType",
                table: "IngestedSourceDocument",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocType",
                table: "IngestedSourceDocument");
        }
    }
}
