using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710020000_ScheduleTemplateNameUniqueActiveOnly")]
    public partial class ScheduleTemplateNameUniqueActiveOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cho phép tái sử dụng tên `ScheduleTemplate` sau khi deactivate:
            // unique chỉ trong phạm vi các row IsActive = 1.
            migrationBuilder.DropIndex(
                name: "IX_ScheduleTemplates_Name",
                table: "ScheduleTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_Name",
                table: "ScheduleTemplates",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleTemplates_Name",
                table: "ScheduleTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_Name",
                table: "ScheduleTemplates",
                column: "Name",
                unique: true);
        }
    }
}
