using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260705110000_ScheduleTemplateMultipleShifts")]
    public partial class ScheduleTemplateMultipleShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleTemplateShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleTemplateId = table.Column<int>(type: "int", nullable: false),
                    ShiftTemplateId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTemplateShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplateShifts_ScheduleTemplates_ScheduleTemplateId",
                        column: x => x.ScheduleTemplateId,
                        principalTable: "ScheduleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplateShifts_ShiftTemplates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "ShiftTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplateShifts_ScheduleTemplateId_ShiftTemplateId",
                table: "ScheduleTemplateShifts",
                columns: new[] { "ScheduleTemplateId", "ShiftTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplateShifts_ShiftTemplateId",
                table: "ScheduleTemplateShifts",
                column: "ShiftTemplateId");

            migrationBuilder.Sql("""
                INSERT INTO [ScheduleTemplateShifts] ([ScheduleTemplateId], [ShiftTemplateId])
                SELECT [Id], [ShiftTemplateId]
                FROM [ScheduleTemplates] st
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [ScheduleTemplateShifts] sts
                    WHERE sts.[ScheduleTemplateId] = st.[Id]
                      AND sts.[ShiftTemplateId] = st.[ShiftTemplateId]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleTemplateShifts");
        }
    }
}
