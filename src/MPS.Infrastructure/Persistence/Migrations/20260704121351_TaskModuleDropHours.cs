using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskModuleDropHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "ProjectTasks");

            migrationBuilder.RenameColumn(
                name: "EstimatedHoursPerUnit",
                table: "ProjectTasks",
                newName: "Weight");

            migrationBuilder.RenameColumn(
                name: "EstimatedHoursPerUnit",
                table: "ProcessSteps",
                newName: "DefaultWeight");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Weight",
                table: "ProjectTasks",
                newName: "EstimatedHoursPerUnit");

            migrationBuilder.RenameColumn(
                name: "DefaultWeight",
                table: "ProcessSteps",
                newName: "EstimatedHoursPerUnit");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedHours",
                table: "ProjectTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
