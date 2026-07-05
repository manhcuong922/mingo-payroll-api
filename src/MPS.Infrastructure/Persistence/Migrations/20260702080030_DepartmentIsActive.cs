using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsActive",
                table: "Departments",
                column: "IsActive");

            // Seed 5 phòng ban mẫu — IDEMPOTENT: chỉ chèn phòng có Code chưa tồn tại.
            // Tránh xung đột với dữ liệu người dùng đã tạo (vd DB đã có sẵn IT/BGD). ManagerId để null
            // (gán sau khi có nhân sự + tài khoản — Lưu ý #9). Không chèn Id cứng (Code là unique key).
            migrationBuilder.Sql(@"
INSERT INTO [Departments] ([Code], [Name], [Description], [ManagerId], [IsActive])
SELECT v.[Code], v.[Name], v.[Description], NULL, 1
FROM (VALUES
    (N'IT',  N'Phòng Công nghệ thông tin', N'Quản trị hệ thống, phát triển phần mềm nội bộ.'),
    (N'KT',  N'Phòng Kế toán',             N'Kế toán, tài chính, thanh toán lương.'),
    (N'NS',  N'Phòng Nhân sự',             N'Tuyển dụng, hồ sơ nhân sự, chế độ phúc lợi.'),
    (N'KD',  N'Phòng Kinh doanh',          N'Phát triển khách hàng, bán hàng, chăm sóc.'),
    (N'BGD', N'Ban Giám đốc',              N'Điều hành và ra quyết định cấp công ty.')
) AS v([Code], [Name], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Departments] d WHERE d.[Code] = v.[Code]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // KHÔNG xóa data phòng ban khi rollback: seed idempotent không phân biệt được
            // hàng do migration chèn với hàng người dùng đã có sẵn (cùng Code). Chỉ gỡ schema.
            migrationBuilder.DropIndex(
                name: "IX_Departments_IsActive",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Departments");
        }
    }
}
