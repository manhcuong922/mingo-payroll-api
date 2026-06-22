using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceAuthoritativeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceAuthoritativeSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetEntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LegalDocumentId = table.Column<int>(type: "int", nullable: true),
                    DocumentNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAuthoritativeSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuthoritativeSources_TargetEntityType",
                table: "ComplianceAuthoritativeSources",
                column: "TargetEntityType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceAuthoritativeSources");
        }
    }
}
