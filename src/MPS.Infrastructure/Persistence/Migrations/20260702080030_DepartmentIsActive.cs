using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentIsActive : Migration
    {
        // Mã các phòng ban / xưởng mẫu — dùng chung cho Up (seed) và Down (xóa) để dễ rollback.
        private static readonly string[] SeedCodes = { "IT", "KT", "NS", "KD", "BGD" };

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

            // Seed 5 phòng ban mẫu. ManagerId để null (gán sau khi có nhân sự + tài khoản — Lưu ý #9).
            // Id auto-increment nên KHÔNG chèn Id cứng, tránh xung đột với dữ liệu người dùng tạo.
            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Code", "Name", "Description", "ManagerId", "IsActive" },
                values: new object[,]
                {
                    { "IT",  "Phòng Công nghệ thông tin", "Quản trị hệ thống, phát triển phần mềm nội bộ.", null, true },
                    { "KT",  "Phòng Kế toán",             "Kế toán, tài chính, thanh toán lương.",          null, true },
                    { "NS",  "Phòng Nhân sự",             "Tuyển dụng, hồ sơ nhân sự, chế độ phúc lợi.",    null, true },
                    { "KD",  "Phòng Kinh doanh",          "Phát triển khách hàng, bán hàng, chăm sóc.",     null, true },
                    { "BGD", "Ban Giám đốc",              "Điều hành và ra quyết định cấp công ty.",        null, true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var code in SeedCodes)
            {
                migrationBuilder.DeleteData(
                    table: "Departments",
                    keyColumn: "Code",
                    keyValue: code);
            }

            migrationBuilder.DropIndex(
                name: "IX_Departments_IsActive",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Departments");
        }
    }
}
