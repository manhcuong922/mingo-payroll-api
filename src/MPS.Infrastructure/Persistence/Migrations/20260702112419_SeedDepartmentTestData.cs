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
        // Bcrypt hash công khai của mật khẩu test "User@123" — KHÔNG phải credential thật (chỉ cho seed demo).
#pragma warning disable S2068 // hard-coded credential — cố ý: hash test dùng chung, đổi mật khẩu sau khi test xong.
        private const string DemoPasswordHash = "$2a$12$1BcbI6FrjjkKHhN6TGfsJuNvrBAZYsZ2NLHh8Qnd7TfpKWi1fsPRG";
#pragma warning restore S2068

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 1. Seed Users demo (idempotent theo Username) — RoleId lấy động theo Roles.Name =====
            migrationBuilder.Sql($@"
INSERT INTO [Users] ([EmployeeCode],[FullName],[RoleId],[Username],[PasswordHash],[Email],[Dob],[PhoneNo],[Status],[PasswordChangedAt])
SELECT v.[EmployeeCode], v.[FullName], r.[Id], v.[Username], '{DemoPasswordHash}', v.[Email],
       '1995-01-01T00:00:00', v.[PhoneNo], 1, SYSUTCDATETIME()
FROM (VALUES
    (N'PMIT001',  N'Tổ trưởng IT',   N'pmit',   N'pmit@mingo.local',   N'0900000101', N'ProjectManager'),
    (N'PMBIZ001', N'Tổ trưởng KD',   N'pmbiz',  N'pmbiz@mingo.local',  N'0900000102', N'ProjectManager'),
    (N'CN001',    N'Công nhân IT 1', N'cnit1',  N'cnit1@mingo.local',  N'0900000201', N'Employee'),
    (N'CN002',    N'Công nhân IT 2', N'cnit2',  N'cnit2@mingo.local',  N'0900000202', N'Employee'),
    (N'CN003',    N'Công nhân KD 1', N'cnbiz1', N'cnbiz1@mingo.local', N'0900000203', N'Employee'),
    (N'CN004',    N'Công nhân KD 2', N'cnbiz2', N'cnbiz2@mingo.local', N'0900000204', N'Employee'),
    (N'CN005',    N'Nhân viên HR 1', N'cnhr1',  N'cnhr1@mingo.local',  N'0900000205', N'Employee')
) AS v([EmployeeCode],[FullName],[Username],[Email],[PhoneNo],[RoleName])
JOIN [Roles] r ON r.[Name] = v.[RoleName]
WHERE NOT EXISTS (SELECT 1 FROM [Users] u WHERE u.[Username] = v.[Username]);
");

            // ===== 2. Seed EmployeeInformation (idempotent theo CitizenId). Position: MIN Id để thỏa FK. =====
            migrationBuilder.Sql(@"
DECLARE @DefaultPositionId INT = (SELECT MIN([Id]) FROM [Positions]);

INSERT INTO [EmployeeInformations]
    ([UserId],[FullName],[Dob],[Gender],[CitizenId],[PermanentAddress],[Email],[PhoneNo],[PositionId],[DepartmentId],[DependentsNo])
SELECT u.[Id], u.[FullName], '1995-01-01T00:00:00', 0, v.[CitizenId],
       N'N/A (test seed)', u.[Email], u.[PhoneNo], @DefaultPositionId, d.[Id], 0
FROM (VALUES
    (N'pmit',   N'TEST000000001', N'IT'),
    (N'pmbiz',  N'TEST000000002', N'BIZ'),
    (N'cnit1',  N'TEST000000003', N'IT'),
    (N'cnit2',  N'TEST000000004', N'IT'),
    (N'cnbiz1', N'TEST000000005', N'BIZ'),
    (N'cnbiz2', N'TEST000000006', N'BIZ'),
    (N'cnhr1',  N'TEST000000007', N'HR')
) AS v([Username],[CitizenId],[DeptCode])
JOIN [Users] u ON u.[Username] = v.[Username]
JOIN [Departments] d ON d.[Code] = v.[DeptCode]
WHERE @DefaultPositionId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [EmployeeInformations] e WHERE e.[CitizenId] = v.[CitizenId]);
");

            // ===== 3. Đặt tổ trưởng IT + BIZ (chỉ khi xưởng chưa có ManagerId) =====
            migrationBuilder.Sql(@"
UPDATE d SET d.[ManagerId] = e.[Id]
FROM [Departments] d
JOIN [Users] u ON u.[Username] = 'pmit'
JOIN [EmployeeInformations] e ON e.[UserId] = u.[Id]
WHERE d.[Code] = 'IT' AND d.[ManagerId] IS NULL;
");
            migrationBuilder.Sql(@"
UPDATE d SET d.[ManagerId] = e.[Id]
FROM [Departments] d
JOIN [Users] u ON u.[Username] = 'pmbiz'
JOIN [EmployeeInformations] e ON e.[UserId] = u.[Id]
WHERE d.[Code] = 'BIZ' AND d.[ManagerId] IS NULL;
");
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
