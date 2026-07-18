using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskModuleProgressV8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlannedWorkUnits",
                table: "ProjectTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequiredGate",
                table: "ProjectTasks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPlannedQuantity",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPlannedWorkUnits",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill dữ liệu cũ: mọi Milestone hiện có coi là gate bắt buộc (an toàn hơn optional —
            // xem docs/plans/plan.md mục 9.5). Task/Stage Quantity không đổi hành vi.
            migrationBuilder.Sql(@"UPDATE [ProjectTasks] SET [IsRequiredGate] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedWorkUnits",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "IsRequiredGate",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "DefaultPlannedQuantity",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "DefaultPlannedWorkUnits",
                table: "ProcessSteps");
        }
    }
}
