using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.Runtime.Rules.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuleControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanaryRollouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TargetTenantIds = table.Column<string[]>(type: "text[]", nullable: false),
                    TargetPercentage = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PromotedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RolledBackBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RollbackReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanaryRollouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleSetAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetTenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SignatureAlgorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SignatureKeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSetAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RejectedReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantQuotaOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetTenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxRequestsPerDay = table.Column<int>(type: "integer", nullable: true),
                    MaxConcurrentRules = table.Column<int>(type: "integer", nullable: true),
                    MaxWorkflows = table.Column<int>(type: "integer", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantQuotaOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantRuleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetTenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AssignedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRuleAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanaryRollouts_TenantId_WorkflowName_Status",
                table: "CanaryRollouts",
                columns: new[] { "TenantId", "WorkflowName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleSetAudits_EventType",
                table: "RuleSetAudits",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSetAudits_TenantId_WorkflowName_OccurredAt",
                table: "RuleSetAudits",
                columns: new[] { "TenantId", "WorkflowName", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleSets_TenantId_WorkflowName_IsActive",
                table: "RuleSets",
                columns: new[] { "TenantId", "WorkflowName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleSets_TenantId_WorkflowName_Version",
                table: "RuleSets",
                columns: new[] { "TenantId", "WorkflowName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantQuotaOverrides_TenantId_TargetTenantId",
                table: "TenantQuotaOverrides",
                columns: new[] { "TenantId", "TargetTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantRuleAssignments_TenantId_TargetTenantId_WorkflowName",
                table: "TenantRuleAssignments",
                columns: new[] { "TenantId", "TargetTenantId", "WorkflowName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanaryRollouts");

            migrationBuilder.DropTable(
                name: "RuleSetAudits");

            migrationBuilder.DropTable(
                name: "RuleSets");

            migrationBuilder.DropTable(
                name: "TenantQuotaOverrides");

            migrationBuilder.DropTable(
                name: "TenantRuleAssignments");
        }
    }
}
