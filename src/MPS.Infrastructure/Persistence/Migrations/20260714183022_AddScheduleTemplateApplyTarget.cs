using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTemplateApplyTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplyTarget",
                table: "ScheduleTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetDepartmentIds",
                table: "ScheduleTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyTarget",
                table: "ScheduleTemplates");

            migrationBuilder.DropColumn(
                name: "TargetDepartmentIds",
                table: "ScheduleTemplates");
        }
    }
}
