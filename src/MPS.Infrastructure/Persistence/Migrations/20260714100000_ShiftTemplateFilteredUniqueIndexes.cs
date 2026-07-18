using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260714100000_ShiftTemplateFilteredUniqueIndexes")]
    public partial class ShiftTemplateFilteredUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cho phép tái sử dụng Code / Name của ShiftTemplate sau khi deactivate:
            // unique chỉ trong phạm vi các row IsActive = 1.
            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_Code",
                table: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_Name",
                table: "ShiftTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Code",
                table: "ShiftTemplates",
                column: "Code",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Name",
                table: "ShiftTemplates",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_Code",
                table: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_Name",
                table: "ShiftTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Code",
                table: "ShiftTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Name",
                table: "ShiftTemplates",
                column: "Name",
                unique: true);
        }
    }
}
