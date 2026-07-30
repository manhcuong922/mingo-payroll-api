using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260714110000_AttendanceRuleFilteredUniqueIndex")]
    public partial class AttendanceRuleFilteredUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cho phép tái sử dụng Name của AttendanceRule sau khi deactivate:
            // unique chỉ trong phạm vi các row IsActive = 1.
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRules_Name",
                table: "AttendanceRules");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRules_Name",
                table: "AttendanceRules",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRules_Name",
                table: "AttendanceRules");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRules_Name",
                table: "AttendanceRules",
                column: "Name",
                unique: true);
        }
    }
}
