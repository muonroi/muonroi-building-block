using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfTemplateDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfTemplateApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfTemplateApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PdfTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_PdfTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PdfTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RejectedReason = table.Column<string>(type: "text", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfTemplateVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplateApprovals_TenantId_TemplateId_Version",
                table: "PdfTemplateApprovals",
                columns: new[] { "TenantId", "TemplateId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplateApprovals_TenantId_TemplateVersionId",
                table: "PdfTemplateApprovals",
                columns: new[] { "TenantId", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplates_TenantId_IsActive",
                table: "PdfTemplates",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplates_TenantId_TemplateId",
                table: "PdfTemplates",
                columns: new[] { "TenantId", "TemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplateVersions_TenantId_TemplateId_Status",
                table: "PdfTemplateVersions",
                columns: new[] { "TenantId", "TemplateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplateVersions_TenantId_TemplateId_Version",
                table: "PdfTemplateVersions",
                columns: new[] { "TenantId", "TemplateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfTemplateApprovals");

            migrationBuilder.DropTable(
                name: "PdfTemplates");

            migrationBuilder.DropTable(
                name: "PdfTemplateVersions");
        }
    }
}
