using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypePerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypePerformanceMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Dob = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhoneNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TypePerformanceMetricId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceMetrics_TypePerformanceMetrics_TypePerformanceMetricId",
                        column: x => x.TypePerformanceMetricId,
                        principalTable: "TypePerformanceMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeInformations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TaxNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SocialNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    DependentsNo = table.Column<int>(type: "int", nullable: false),
                    CitizenId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeInformations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeInformations_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInformations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceMetricRoles",
                columns: table => new
                {
                    PerformanceMetricId = table.Column<int>(type: "int", nullable: false),
                    TargetRolesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceMetricRoles", x => new { x.PerformanceMetricId, x.TargetRolesId });
                    table.ForeignKey(
                        name: "FK_PerformanceMetricRoles_PerformanceMetrics_PerformanceMetricId",
                        column: x => x.PerformanceMetricId,
                        principalTable: "PerformanceMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceMetricRoles_Roles_TargetRolesId",
                        column: x => x.TargetRolesId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 1, "Read", "User.Read.All", "Xem tất cả người dùng.", "User", "All" },
                    { 2, "Read", "User.Read.Self", "Xem thông tin tài khoản của chính mình.", "User", "Self" },
                    { 3, "Create", "User.Create.All", "Tạo người dùng mới.", "User", "All" },
                    { 4, "Update", "User.Update.All", "Cập nhật bất kỳ người dùng nào.", "User", "All" },
                    { 5, "Update", "User.Update.Self", "Cập nhật thông tin của chính mình.", "User", "Self" },
                    { 6, "Delete", "User.Delete.All", "Xóa người dùng.", "User", "All" },
                    { 7, "Read", "EmployeeInformation.Read.All", "Xem hồ sơ nhân sự của tất cả nhân viên.", "EmployeeInformation", "All" },
                    { 8, "Read", "EmployeeInformation.Read.Self", "Xem hồ sơ nhân sự của chính mình.", "EmployeeInformation", "Self" },
                    { 9, "Create", "EmployeeInformation.Create.All", "Tạo hồ sơ nhân sự.", "EmployeeInformation", "All" },
                    { 10, "Update", "EmployeeInformation.Update.All", "Cập nhật hồ sơ nhân sự của bất kỳ ai.", "EmployeeInformation", "All" },
                    { 11, "Update", "EmployeeInformation.Update.Self", "Cập nhật hồ sơ nhân sự của chính mình.", "EmployeeInformation", "Self" },
                    { 12, "Delete", "EmployeeInformation.Delete.All", "Xóa hồ sơ nhân sự.", "EmployeeInformation", "All" },
                    { 13, "Read", "Position.Read.All", "Xem danh sách chức vụ.", "Position", "All" },
                    { 14, "Create", "Position.Create.All", "Tạo chức vụ.", "Position", "All" },
                    { 15, "Update", "Position.Update.All", "Cập nhật chức vụ.", "Position", "All" },
                    { 16, "Delete", "Position.Delete.All", "Xóa chức vụ.", "Position", "All" },
                    { 17, "Read", "PerformanceMetric.Read.All", "Xem chỉ tiêu KPI của tất cả nhân viên.", "PerformanceMetric", "All" },
                    { 18, "Read", "PerformanceMetric.Read.Self", "Xem KPI của chính mình.", "PerformanceMetric", "Self" },
                    { 19, "Create", "PerformanceMetric.Create.All", "Tạo chỉ tiêu KPI.", "PerformanceMetric", "All" },
                    { 20, "Update", "PerformanceMetric.Update.All", "Cập nhật chỉ tiêu KPI.", "PerformanceMetric", "All" },
                    { 21, "Delete", "PerformanceMetric.Delete.All", "Xóa chỉ tiêu KPI.", "PerformanceMetric", "All" },
                    { 22, "Approve", "PerformanceMetric.Approve.All", "Duyệt KPI.", "PerformanceMetric", "All" },
                    { 23, "Read", "TypePerformanceMetric.Read.All", "Xem loại KPI.", "TypePerformanceMetric", "All" },
                    { 24, "Create", "TypePerformanceMetric.Create.All", "Tạo loại KPI.", "TypePerformanceMetric", "All" },
                    { 25, "Update", "TypePerformanceMetric.Update.All", "Cập nhật loại KPI.", "TypePerformanceMetric", "All" },
                    { 26, "Delete", "TypePerformanceMetric.Delete.All", "Xóa loại KPI.", "TypePerformanceMetric", "All" },
                    { 27, "Read", "Role.Read.All", "Xem role và permission.", "Role", "All" },
                    { 28, "Create", "Role.Create.All", "Tạo role mới.", "Role", "All" },
                    { 29, "Update", "Role.Update.All", "Cập nhật role (chặn system role).", "Role", "All" },
                    { 30, "Delete", "Role.Delete.All", "Xóa role (chặn system role hoặc role còn user).", "Role", "All" },
                    { 31, "ManagePermissions", "Role.ManagePermissions.All", "Gán/sửa permission của role.", "Role", "All" },
                    { 32, "Revoke", "RefreshToken.Revoke.All", "Force logout user khác.", "RefreshToken", "All" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsSystem", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quản trị hệ thống — toàn quyền.", true, "SystemAdmin", null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phòng Nhân sự — quản lý nhân viên & thông tin nhân sự.", true, "HR", null },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kế toán — đọc thông tin lương/thuế nhân viên.", true, "Accountant", null },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Giám đốc — duyệt KPI, quản lý phòng ban.", true, "Director", null },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Trưởng phòng — quản lý KPI nhân viên trong phòng.", true, "DepartmentManager", null },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quản lý dự án — theo dõi KPI dự án.", true, "ProjectManager", null },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nhân viên — chỉ xem/sửa thông tin của chính mình.", true, "Employee", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 1 },
                    { 17, 1 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 1 },
                    { 21, 1 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 1 },
                    { 25, 1 },
                    { 26, 1 },
                    { 27, 1 },
                    { 28, 1 },
                    { 29, 1 },
                    { 30, 1 },
                    { 31, 1 },
                    { 32, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 5, 2 },
                    { 6, 2 },
                    { 7, 2 },
                    { 8, 2 },
                    { 9, 2 },
                    { 10, 2 },
                    { 11, 2 },
                    { 12, 2 },
                    { 13, 2 },
                    { 14, 2 },
                    { 15, 2 },
                    { 16, 2 },
                    { 17, 2 },
                    { 18, 2 },
                    { 23, 2 },
                    { 2, 3 },
                    { 5, 3 },
                    { 7, 3 },
                    { 8, 3 },
                    { 11, 3 },
                    { 13, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 5, 4 },
                    { 7, 4 },
                    { 8, 4 },
                    { 11, 4 },
                    { 13, 4 },
                    { 14, 4 },
                    { 15, 4 },
                    { 16, 4 },
                    { 17, 4 },
                    { 18, 4 },
                    { 22, 4 },
                    { 23, 4 },
                    { 24, 4 },
                    { 25, 4 },
                    { 26, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 5, 5 },
                    { 7, 5 },
                    { 8, 5 },
                    { 11, 5 },
                    { 13, 5 },
                    { 17, 5 },
                    { 18, 5 },
                    { 19, 5 },
                    { 20, 5 },
                    { 21, 5 },
                    { 22, 5 },
                    { 23, 5 },
                    { 2, 6 },
                    { 5, 6 },
                    { 8, 6 },
                    { 11, 6 },
                    { 13, 6 },
                    { 17, 6 },
                    { 18, 6 },
                    { 19, 6 },
                    { 20, 6 },
                    { 21, 6 },
                    { 23, 6 },
                    { 2, 7 },
                    { 5, 7 },
                    { 8, 7 },
                    { 11, 7 },
                    { 13, 7 },
                    { 18, 7 },
                    { 23, 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_PositionId",
                table: "EmployeeInformations",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_UserId",
                table: "EmployeeInformations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMetricRoles_TargetRolesId",
                table: "PerformanceMetricRoles",
                column: "TargetRolesId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMetrics_Name",
                table: "PerformanceMetrics",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceMetrics_TypePerformanceMetricId",
                table: "PerformanceMetrics",
                column: "TypePerformanceMetricId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Resource_Action_Scope",
                table: "Permissions",
                columns: new[] { "Resource", "Action", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeCode",
                table: "Users",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeInformations");

            migrationBuilder.DropTable(
                name: "PerformanceMetricRoles");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "PerformanceMetrics");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "TypePerformanceMetrics");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
