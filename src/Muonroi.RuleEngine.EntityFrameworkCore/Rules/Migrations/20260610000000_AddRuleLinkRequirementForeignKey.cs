#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleLinkRequirementForeignKey : Migration
    {
        // WR-01 (03-REVIEW.md): add the requirement↔rule referential-integrity edge at the DB level.
        // Before this FK an orphan RuleLink (RequirementId referencing a deleted/non-existent
        // Requirement) could be persisted and was then silently dropped from the matrix
        // (TraceabilityMatrixBuilder.resolveRequirements) — an invisible hole. The FK makes orphan
        // links impossible. ON DELETE CASCADE aligns with D-01 (links are mutable/deletable):
        // deleting a requirement removes its links rather than leaving them dangling. The FK rides
        // on the existing IX_RuleLinks_RequirementId index created in AddTraceabilityEntities.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_RuleLinks_Requirements_RequirementId",
                table: "RuleLinks",
                column: "RequirementId",
                principalTable: "Requirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RuleLinks_Requirements_RequirementId",
                table: "RuleLinks");
        }
    }
}
