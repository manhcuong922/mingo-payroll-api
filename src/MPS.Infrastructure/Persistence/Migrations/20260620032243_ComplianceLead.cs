using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceLeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetEntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaimedDocumentNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimedEffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaimedSourceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClaimSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProposalId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceLeads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceLeads_Status_CreatedAt",
                table: "ComplianceLeads",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceLeads");
        }
    }
}
