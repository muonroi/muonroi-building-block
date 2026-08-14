#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    // Phase 17 / D-01 (TRACE-01/02). Adds the source-document discriminator to the existing
    // CopilotDraftProvenance table: nullable SourceDocumentId (uuid) + nullable SourceRef
    // (varchar 512). Existing rows read as non-ingested (null = NL-copilot / manual path);
    // the ingestion path stamps document.Id / document.SourceRef. No RLS block and no
    // parity-array edit — CopilotDraftProvenance is already RLS-enabled with a dual
    // USING + WITH CHECK tenant_isolation policy and is already in the pg_policies parity
    // ARRAY (20260613011007_CatchUpRlsProvenanceWriteCheck / 20260614000000 :73).
    public partial class AddSourceDocToCopilotProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "CopilotDraftProvenance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRef",
                table: "CopilotDraftProvenance",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "CopilotDraftProvenance");

            migrationBuilder.DropColumn(
                name: "SourceRef",
                table: "CopilotDraftProvenance");
        }
    }
}
