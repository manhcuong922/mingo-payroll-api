using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiServiceRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsSystem", "Name", "UpdatedAt" },
                values: new object[] { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AI Service account — chấm công và đọc embedding từ camera nhận diện khuôn mặt.", true, "AiService", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
