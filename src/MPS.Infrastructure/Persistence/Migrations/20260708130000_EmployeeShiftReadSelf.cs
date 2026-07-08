using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708130000_EmployeeShiftReadSelf")]
    public partial class EmployeeShiftReadSelf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NV cần xem lịch phân ca kế hoạch của chính mình (bổ sung cho Attendance.Read.Self
            // vốn chỉ trả AttendanceRecord đã chấm công).
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope]) VALUES
    (179, N'Read', N'EmployeeShift.Read.Self',
     N'Xem lịch phân ca kế hoạch của chính mình.',
     N'EmployeeShift', N'Self');
SET IDENTITY_INSERT [Permissions] OFF;");

            // Cấp cho Employee (7) — role duy nhất cần self view.
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (179, 7);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 179;
DELETE FROM [Permissions] WHERE [Id] = 179;");
        }
    }
}
