using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seed dữ liệu TEST cho module Department (độc lập với DatabaseSeeder — KHÔNG cần config password):
    ///   - User demo: 2 tổ trưởng (ProjectManager) + 5 công nhân (Employee), password chung "User@123".
    ///   - EmployeeInformation cho từng user, gắn DepartmentId (IT / BIZ / HR).
    ///   - Đặt ManagerId cho xưởng IT + BIZ = tổ trưởng tương ứng.
    ///
    /// IDEMPOTENT: mọi INSERT đều WHERE NOT EXISTS theo Username / CitizenId; KHÔNG hardcode Id
    /// (dùng subquery lấy Id động từ Roles/Departments/Positions/Users). Chạy lại nhiều lần an toàn.
    ///
    /// Phụ thuộc: Roles (Name), Departments (Code IT/BIZ/HR), Positions phải tồn tại trước — đều đã
    /// được seed bởi DatabaseSeeder / migration trước. Nếu thiếu, INSERT tương ứng tự bỏ qua (JOIN rỗng).
    ///
    /// Password hash: bcrypt $2a$12$ của "User@123" (BCrypt.Net verify được).
    /// </summary>
    public partial class SeedDepartmentTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ VÔ HIỆU HOÁ (2026-07-16): seed test-data (pmit/pmbiz/cnit*/cnbiz*/cnhr1 + hash cứng
            // "User@123") đã được GỠ khỏi migration để thống nhất về MỘT nguồn seed duy nhất là runtime
            // DatabaseSeeder. Nhân sự demo giờ do SeedManufacturingWorkforceDemoAsync / SeedOfficeWorkforceDemoAsync
            // tạo (mật khẩu chung Test@1234). Giữ lại class rỗng để không phá lịch sử migration.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gỡ ManagerId trỏ tới EmployeeInformation demo (tránh vướng FK) → xóa EmployeeInformation → Users.
            migrationBuilder.Sql(@"
UPDATE d SET d.[ManagerId] = NULL
FROM [Departments] d
JOIN [EmployeeInformations] e ON e.[Id] = d.[ManagerId]
WHERE e.[CitizenId] LIKE 'TEST%';
");
            migrationBuilder.Sql(@"DELETE FROM [EmployeeInformations] WHERE [CitizenId] LIKE 'TEST%';");
            migrationBuilder.Sql(@"DELETE FROM [Users] WHERE [Username] IN ('pmit','pmbiz','cnit1','cnit2','cnbiz1','cnbiz2','cnhr1');");
        }
    }
}
