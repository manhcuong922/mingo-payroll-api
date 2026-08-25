using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PieceWorkSupportBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PieceWorkSupportSnapshotJson",
                table: "PayrollPeriods",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PieceWorkSupportBands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinHours = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    MaxHours = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceWorkSupportBands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PieceWorkSupportBands_IsActive_MinHours",
                table: "PieceWorkSupportBands",
                columns: new[] { "IsActive", "MinHours" });

            // Seed 2 bậc mặc định (quyết định PO 18/08/2026: từ 150h = 200.000đ, từ 200h = 500.000đ).
            //
            // Phải làm ở migration chứ không chỉ ở DatabaseSeeder: prod chạy với
            // SEED_RESET_ON_STARTUP=false nên seeder không dựng dữ liệu mới. Không có 2 hàng này thì
            // mọi HĐ khoán nhận 0đ hỗ trợ mà không có lỗi nào báo ra.
            //
            // Idempotent bằng NOT EXISTS — chạy lại (hoặc chạy sau seeder trên máy dev) không nhân đôi.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM PieceWorkSupportBands)
BEGIN
    INSERT INTO PieceWorkSupportBands (MinHours, MaxHours, Amount, Label, IsActive, CreatedAt)
    VALUES (150, 200, 200000, N'Từ 150 giờ', 1, SYSUTCDATETIME()),
           (200, NULL, 500000, N'Từ 200 giờ', 1, SYSUTCDATETIME());
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PieceWorkSupportBands");

            migrationBuilder.DropColumn(
                name: "PieceWorkSupportSnapshotJson",
                table: "PayrollPeriods");
        }
    }
}
